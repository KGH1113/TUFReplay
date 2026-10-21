using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsCGEventInputEventSource : INativeInputEventSource, INativeInputCaptureHealth
{
  private const int BatchCapacity = 256;
  private static readonly int[] PhysicalKeyCodes = CreatePhysicalKeyCodes();
  private readonly object _lifecycleGate = new object();
  private readonly MacOsCGEventNativeEvent[] _events = new MacOsCGEventNativeEvent[BatchCapacity];
  private readonly byte[] _snapshot = new byte[MacOsNativeInputKey.Capacity];
  private MacOsCGEventNativeLibrary _library;
  private MacOsEventTimeConverter _clock;
  private IntPtr _context;
  private Action<NativeInputTransition> _onTransition;
  private Thread _worker;
  private GCHandle _eventPin,
    _statePin;
  private volatile bool _running,
    _stopping;
  private Exception _workerFailure;
  private long _managedDropped;
  private NativeInputSourceDiagnostics _lastDiagnostics;

  public string Name => "macos-cgevent-session-v1";
  public bool UsesExtendedKeyState => false;
  public IReadOnlyList<int> SnapshotKeyCodes => PhysicalKeyCodes;
  public bool IsRunning
  {
    get
    {
      lock (_lifecycleGate)
        return _running && _workerFailure == null && _context != IntPtr.Zero && _library.IsRunning(_context);
    }
  }

  public void Start(Action<NativeInputTransition> onTransition)
  {
    if (onTransition == null)
      throw new ArgumentNullException(nameof(onTransition));
    lock (_lifecycleGate)
    {
      if (_worker != null)
        return;
      if (!MacOsCGEventNativeLibrary.TryLoad(out _library, out string failure))
      {
        MacOsInputMonitoringAccess.NotifyUnavailable(failure);
        throw new DllNotFoundException(failure);
      }
      if (_library.CheckAccess() != MacOsInputAccess.Granted)
      {
        MacOsInputMonitoringAccess.NotifyUnavailable("input_monitoring_denied");
        throw new UnauthorizedAccessException("input_monitoring_denied");
      }
      _context = _library.Create();
      if (_context == IntPtr.Zero)
        throw new InvalidOperationException("macos_cgevent_context_create_failed");
      _onTransition = onTransition;
      _workerFailure = null;
      _managedDropped = 0;
      _stopping = false;
      _eventPin = GCHandle.Alloc(_events, GCHandleType.Pinned);
      _statePin = GCHandle.Alloc(_snapshot, GCHandleType.Pinned);
      try
      {
        _clock = new MacOsEventTimeConverter(_library);
        MacOsInputError error = _library.Start(_context);
        if (error != MacOsInputError.None || !_library.IsRunning(_context))
        {
          string detail = MacOsCGEventErrorFormatter.Format(error, _library.LastSystemError(_context));
          MacOsInputMonitoringAccess.NotifyUnavailable("macos_cgevent_start_failed: " + detail);
          throw new InvalidOperationException("macos_cgevent_start_failed: " + detail);
        }
        RefreshPhysicalState();
        _running = true;
        _worker = new Thread(RunWorker) { IsBackground = true, Name = "TUFReplay CGEvent bridge" };
        _worker.Start();
      }
      catch
      {
        Cleanup();
        throw;
      }
    }
  }

  public void Stop()
  {
    lock (_lifecycleGate)
    {
      _stopping = true;
      _running = false;
      if (_context == IntPtr.Zero)
        return;
      _library.Stop(_context);
      // Never free the context or pinned buffers while the bridge can still
      // use them. This is a separate worker, not the OS event-tap callback.
      if (_worker != null && Thread.CurrentThread != _worker)
        _worker.Join();
      _worker = null;
      Cleanup();
      _onTransition = null;
      _clock = null;
      Array.Clear(_snapshot, 0, _snapshot.Length);
    }
  }

  public void RefreshPhysicalState()
  {
    lock (_lifecycleGate)
    {
      if (_context == IntPtr.Zero || !_statePin.IsAllocated)
        return;
      if (_library.CopyState(_context, _statePin.AddrOfPinnedObject(), _snapshot.Length) != _snapshot.Length)
        throw new InvalidOperationException("macos_cgevent_snapshot_failed");
    }
  }

  public bool TryGetPhysicalKeyState(int key, out bool isDown)
  {
    lock (_lifecycleGate)
    {
      isDown = key >= 0 && key < _snapshot.Length && _snapshot[key] != 0;
      return key >= 0 && key < _snapshot.Length;
    }
  }

  public long ConsumeDroppedEvents()
  {
    lock (_lifecycleGate)
    {
      ulong native = _context == IntPtr.Zero ? 0 : _library.TakeDropped(_context);
      long managed = Interlocked.Exchange(ref _managedDropped, 0);
      return native >= (ulong)(long.MaxValue - managed) ? long.MaxValue : (long)native + managed;
    }
  }

  public string ConsumeCaptureFailure()
  {
    lock (_lifecycleGate)
    {
      if (_workerFailure != null)
        return ReplayUnavailableReasons.InputSourceStopped;
      MacOsCaptureFaults faults = _context == IntPtr.Zero ? MacOsCaptureFaults.None : _library.TakeFaults(_context);
      if ((faults & MacOsCaptureFaults.TapTimeout) != 0)
        return ReplayUnavailableReasons.InputTapTimeout;
      if ((faults & MacOsCaptureFaults.TapDisabled) != 0)
        return ReplayUnavailableReasons.InputTapDisabled;
      if ((faults & MacOsCaptureFaults.EventDelayed) != 0)
        return ReplayUnavailableReasons.InputEventDelayed;
      return null;
    }
  }

  public NativeInputSourceDiagnostics GetDiagnostics()
  {
    lock (_lifecycleGate)
    {
      if (_context == IntPtr.Zero)
        return _lastDiagnostics;
      MacOsCGEventNativeStats stats = _library.GetStats(_context);
      return new NativeInputSourceDiagnostics(
        Saturate(stats.CallbackCount),
        Saturate(stats.RepeatCount),
        Saturate(stats.UnmappedCount),
        0,
        (int)stats.QueueDepth
      );
    }
  }

  private void RunWorker()
  {
    try
    {
      IntPtr destination = _eventPin.AddrOfPinnedObject();
      while (!_stopping)
      {
        int count = _library.WaitDequeue(_context, destination, _events.Length, 100);
        for (int i = 0; i < count; i++)
        {
          MacOsCGEventNativeEvent input = _events[i];
          if (input.KeyCode >= MacOsNativeInputKey.Capacity)
          {
            Interlocked.Increment(ref _managedDropped);
            continue;
          }
          try
          {
            _onTransition?.Invoke(
              new NativeInputTransition(
                _clock.ToStopwatchTicks(input.TimestampNs),
                Saturate(input.TimestampNs),
                (int)input.KeyCode,
                input.Down != 0,
                false,
                (int)input.KeyCode,
                input.ModifierFlags
              )
            );
          }
          catch
          {
            Interlocked.Increment(ref _managedDropped);
          }
        }
        if (!_stopping && !_library.IsRunning(_context))
          throw new InvalidOperationException("macOS CGEvent capture thread stopped.");
      }
    }
    catch (Exception exception)
    {
      _workerFailure = exception;
      _running = false;
      MacOsInputMonitoringAccess.NotifyUnavailable("macos_cgevent_bridge_stopped: " + exception.Message);
    }
  }

  private void Cleanup()
  {
    if (_context != IntPtr.Zero)
    {
      _lastDiagnostics = GetDiagnostics();
      _library.Destroy(_context);
      _context = IntPtr.Zero;
    }
    if (_eventPin.IsAllocated)
      _eventPin.Free();
    if (_statePin.IsAllocated)
      _statePin.Free();
  }

  private static long Saturate(ulong value) => value >= long.MaxValue ? long.MaxValue : (long)value;

  private static int[] CreatePhysicalKeyCodes()
  {
    var keys = new int[MacOsNativeInputKey.Capacity];
    for (int i = 0; i < keys.Length; i++)
      keys[i] = i;
    return keys;
  }
}
