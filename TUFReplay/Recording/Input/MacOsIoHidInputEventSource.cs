using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsIoHidInputEventSource : INativeInputEventSource
{
  private const int NativeBatchCapacity = 256;
  private const int NativeUsageCapacity = 256;
  private const int KeyStateCapacity = 256;
  private static readonly int[] PhysicalKeyCodes = CreatePhysicalKeyCodes();

  private readonly object _stateGate = new object();
  private readonly MacOsIoHidNativeEvent[] _nativeEvents = new MacOsIoHidNativeEvent[NativeBatchCapacity];
  private readonly byte[] _nativeState = new byte[NativeUsageCapacity];
  private readonly bool[] _usageDown = new bool[NativeUsageCapacity];
  private readonly ushort[] _keyDownCounts = new ushort[KeyStateCapacity];
  private readonly bool[] _snapshotKeyDown = new bool[KeyStateCapacity];

  private MacOsIoHidNativeLibrary _library;
  private MacOsMachTimeConverter _timeConverter;
  private IntPtr _context;
  private Action<NativeInputTransition> _onTransition;
  private Thread _worker;
  private GCHandle _eventPin;
  private GCHandle _statePin;
  private volatile bool _running;
  private volatile bool _stopping;
  private Exception _workerFailure;
  private long _managedDropped;
  private long _managedUnmapped;
  private NativeInputSourceDiagnostics _lastDiagnostics;

  public string Name => "macos-iohid-manager-v1";
  public bool UsesExtendedKeyState => false;
  public IReadOnlyList<int> SnapshotKeyCodes => PhysicalKeyCodes;

  public bool IsRunning
  {
    get
    {
      if (!_running || _workerFailure != null || _context == IntPtr.Zero)
        return false;
      try
      {
        return _library != null && _library.IsRunning(_context);
      }
      catch
      {
        return false;
      }
    }
  }

  public void Start(Action<NativeInputTransition> onTransition)
  {
    if (onTransition == null)
      throw new ArgumentNullException(nameof(onTransition));
    if (_worker != null)
      return;
    if (!MacOsIoHidNativeLibrary.TryLoad(out _library, out string loadFailure))
    {
      MacOsInputMonitoringAccess.NotifyUnavailable(loadFailure);
      throw new DllNotFoundException(loadFailure);
    }
    MacOsInputAccess access = _library.CheckAccess();
    if (access != MacOsInputAccess.Granted)
    {
      MacOsInputMonitoringAccess.NotifyUnavailable("input_monitoring_" + access.ToString().ToLowerInvariant());
      throw new UnauthorizedAccessException("input_monitoring_" + access.ToString().ToLowerInvariant());
    }

    _context = _library.Create();
    if (_context == IntPtr.Zero)
    {
      MacOsInputMonitoringAccess.NotifyUnavailable("macos_iohid_context_create_failed");
      throw new InvalidOperationException("macos_iohid_context_create_failed");
    }

    _onTransition = onTransition;
    _workerFailure = null;
    _managedDropped = 0;
    _managedUnmapped = 0;
    _stopping = false;
    _eventPin = GCHandle.Alloc(_nativeEvents, GCHandleType.Pinned);
    _statePin = GCHandle.Alloc(_nativeState, GCHandleType.Pinned);
    try
    {
      _timeConverter = new MacOsMachTimeConverter(_library);
      MacOsInputError error = _library.Start(_context);
      if (error != MacOsInputError.None || !_library.IsRunning(_context))
      {
        MacOsInputMonitoringAccess.NotifyUnavailable("macos_iohid_start_failed: " + error);
        throw new InvalidOperationException("macos_iohid_start_failed: " + error);
      }
      RefreshPhysicalState();
      _running = true;
      _worker = new Thread(RunWorker) { IsBackground = true, Name = "TUFReplay macOS IOHID Bridge" };
      _worker.Start();
    }
    catch
    {
      CleanupNativeContext();
      throw;
    }
  }

  public void Stop()
  {
    _stopping = true;
    _running = false;
    try
    {
      if (_context != IntPtr.Zero)
        _library?.Stop(_context);
    }
    finally
    {
      if (_worker != null && Thread.CurrentThread != _worker)
        _worker.Join(2000);
      _worker = null;
      DrainRemainingNativeEvents();
      CleanupNativeContext();
      _onTransition = null;
      _timeConverter = null;
      lock (_stateGate)
      {
        Array.Clear(_usageDown, 0, _usageDown.Length);
        Array.Clear(_keyDownCounts, 0, _keyDownCounts.Length);
        Array.Clear(_snapshotKeyDown, 0, _snapshotKeyDown.Length);
      }
    }
  }

  public void RefreshPhysicalState()
  {
    if (_context == IntPtr.Zero || !_statePin.IsAllocated)
      return;
    lock (_stateGate)
    {
      if (_library.CopyState(_context, _statePin.AddrOfPinnedObject(), _nativeState.Length) != NativeUsageCapacity)
        return;
      Array.Clear(_usageDown, 0, _usageDown.Length);
      Array.Clear(_keyDownCounts, 0, _keyDownCounts.Length);
      Array.Clear(_snapshotKeyDown, 0, _snapshotKeyDown.Length);
      for (int usage = 0; usage < _nativeState.Length; usage++)
      {
        if (_nativeState[usage] == 0 || !NativeInputKeyCodeMapper.TryGetMacVirtualKeyFromHidUsage(usage, out int key))
          continue;
        _usageDown[usage] = true;
        if (key < 0 || key >= _keyDownCounts.Length)
          continue;
        if (_keyDownCounts[key] < ushort.MaxValue)
          _keyDownCounts[key]++;
        _snapshotKeyDown[key] = true;
      }
    }
  }

  public bool TryGetPhysicalKeyState(int keyCode, out bool isDown)
  {
    lock (_stateGate)
    {
      if (keyCode < 0 || keyCode >= _snapshotKeyDown.Length)
      {
        isDown = false;
        return false;
      }
      isDown = _snapshotKeyDown[keyCode];
      return true;
    }
  }

  public long ConsumeDroppedEvents()
  {
    ulong nativeDropped = _context == IntPtr.Zero ? 0 : _library.TakeDropped(_context);
    long managedDropped = Interlocked.Exchange(ref _managedDropped, 0);
    if (nativeDropped >= long.MaxValue)
      return long.MaxValue;
    long native = (long)nativeDropped;
    return managedDropped > long.MaxValue - native ? long.MaxValue : native + managedDropped;
  }

  public NativeInputSourceDiagnostics GetDiagnostics()
  {
    if (_context == IntPtr.Zero || _library == null)
      return _lastDiagnostics;
    MacOsIoHidNativeStats stats = _library.GetStats(_context);
    return new NativeInputSourceDiagnostics(
      SaturatingLong(stats.CallbackCount),
      SaturatingLong(stats.RepeatCount),
      SaturatingLong(stats.UnmappedCount) + Interlocked.Read(ref _managedUnmapped),
      unchecked((int)stats.DeviceCount),
      unchecked((int)stats.QueueDepth)
    );
  }

  internal long ManagedUnmapped => Interlocked.Read(ref _managedUnmapped);

  private void RunWorker()
  {
    try
    {
      IntPtr destination = _eventPin.AddrOfPinnedObject();
      while (!_stopping)
      {
        int count = _library.WaitDequeue(_context, destination, _nativeEvents.Length, 100);
        for (int i = 0; i < count; i++)
          ProcessNativeEvent(_nativeEvents[i]);
        if (!_stopping && !_library.IsRunning(_context))
          throw new InvalidOperationException(
            "macOS IOHID native thread stopped unexpectedly: " + _library.LastError(_context)
          );
      }
    }
    catch (Exception exception)
    {
      MacOsInputMonitoringAccess.NotifyUnavailable("macos_iohid_bridge_stopped");
      _workerFailure = exception;
      _running = false;
    }
  }

  private void ProcessNativeEvent(MacOsIoHidNativeEvent nativeEvent)
  {
    int usage = unchecked((int)nativeEvent.Usage);
    if (
      usage < 0
      || usage >= _usageDown.Length
      || !NativeInputKeyCodeMapper.TryGetMacVirtualKeyFromHidUsage(usage, out int key)
      || key < 0
      || key >= _keyDownCounts.Length
    )
    {
      Interlocked.Increment(ref _managedUnmapped);
      return;
    }

    bool down = nativeEvent.Down != 0;
    bool emit;
    lock (_stateGate)
    {
      if (_usageDown[usage] == down)
        return;
      _usageDown[usage] = down;
      ushort count = _keyDownCounts[key];
      bool wasDown = count != 0;
      if (down)
      {
        if (count < ushort.MaxValue)
          count++;
      }
      else if (count != 0)
      {
        count--;
      }
      _keyDownCounts[key] = count;
      bool isDown = count != 0;
      _snapshotKeyDown[key] = isDown;
      emit = wasDown != isDown;
      down = isDown;
    }
    if (!emit)
      return;

    try
    {
      _onTransition?.Invoke(
        new NativeInputTransition(
          _timeConverter.ToStopwatchTicks(nativeEvent.MachTimestamp),
          _timeConverter.ToNanoseconds(nativeEvent.MachTimestamp),
          key,
          down,
          false,
          key,
          nativeEvent.ModifierFlags
        )
      );
    }
    catch
    {
      Interlocked.Increment(ref _managedDropped);
    }
  }

  private void CleanupNativeContext()
  {
    if (_context != IntPtr.Zero)
    {
      try
      {
        _lastDiagnostics = GetDiagnostics();
        _library?.Destroy(_context);
      }
      finally
      {
        _context = IntPtr.Zero;
      }
    }
    if (_eventPin.IsAllocated)
      _eventPin.Free();
    if (_statePin.IsAllocated)
      _statePin.Free();
  }

  private void DrainRemainingNativeEvents()
  {
    if (_context == IntPtr.Zero || !_eventPin.IsAllocated || _library == null)
      return;
    IntPtr destination = _eventPin.AddrOfPinnedObject();
    while (true)
    {
      int count = _library.WaitDequeue(_context, destination, _nativeEvents.Length, 0);
      if (count <= 0)
        return;
      for (int i = 0; i < count; i++)
        ProcessNativeEvent(_nativeEvents[i]);
    }
  }

  private static long SaturatingLong(ulong value) => value >= long.MaxValue ? long.MaxValue : (long)value;

  private static int[] CreatePhysicalKeyCodes()
  {
    HashSet<int> keys = new HashSet<int>();
    for (int usage = 0; usage < NativeUsageCapacity; usage++)
    {
      if (NativeInputKeyCodeMapper.TryGetMacVirtualKeyFromHidUsage(usage, out int key))
        keys.Add(key);
    }
    int[] result = new int[keys.Count];
    keys.CopyTo(result);
    Array.Sort(result);
    return result;
  }
}
