using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Native;

namespace TUFReplay.Recording.Input;

// Records keyboard input through TUFReplayNative.dll. The WH_KEYBOARD_LL procedure
// runs on a native thread that Mono never suspends, so GC pauses and managed work
// no longer delay the system's low-level hook chain, which ADOFAI's async input
// (SkyHook) shares. This managed bridge only drains the native queue.
internal sealed class WindowsNativeKeyboardEventSource : INativeInputEventSource, INativeInputCaptureHealth
{
  private const int BatchCapacity = 256;
  private readonly object _lifecycleGate = new object();
  private readonly WindowsNativeKeyEvent[] _events = new WindowsNativeKeyEvent[BatchCapacity];
  private readonly WindowsNativeLibrary _library;
  private IntPtr _context;
  private Action<NativeInputTransition> _onTransition;
  private WindowsPerformanceCounterConverter _clock;
  private Thread _worker;
  private GCHandle _eventPin;
  private volatile bool _running,
    _stopping;
  private Exception _workerFailure;
  private long _managedDropped;
  private NativeInputSourceDiagnostics _lastDiagnostics;

  private WindowsNativeKeyboardEventSource(WindowsNativeLibrary library)
  {
    _library = library;
  }

  public static bool TryCreate(out INativeInputEventSource source, out string failure)
  {
    if (!WindowsNativeLibrary.TryLoad(out WindowsNativeLibrary library, out failure))
    {
      source = null;
      return false;
    }
    source = new WindowsNativeKeyboardEventSource(library);
    return true;
  }

  public string Name => "windows-native-wh-keyboard-ll-v1";
  public bool UsesExtendedKeyState => true;
  public IReadOnlyList<int> SnapshotKeyCodes => WindowsLowLevelKeyboardEventSource.PhysicalKeys;

  public bool IsRunning
  {
    get
    {
      lock (_lifecycleGate)
        return _running && _workerFailure == null && _library.IsInputRunning(_context);
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
      _context = _library.CreateInput();
      if (_context == IntPtr.Zero)
        throw new InvalidOperationException("windows_native_input_create_failed");
      _onTransition = onTransition;
      _workerFailure = null;
      _managedDropped = 0;
      _stopping = false;
      _eventPin = GCHandle.Alloc(_events, GCHandleType.Pinned);
      try
      {
        // The hook stamps raw QueryPerformanceCounter values; recording compares
        // them with Stopwatch ticks, which Mono counts from a different origin.
        _clock = WindowsPerformanceCounterConverter.CaptureSystemClock();
        WindowsNativeInputError error = _library.StartInput(_context);
        if (error != WindowsNativeInputError.None || !_library.IsInputRunning(_context))
          throw new InvalidOperationException(
            "windows_native_input_start_failed: " + error + " (" + _library.LastInputSystemError(_context) + ")"
          );
        _running = true;
        _worker = new Thread(RunWorker) { IsBackground = true, Name = "TUFReplay Windows input bridge" };
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
      _library.StopInput(_context);
      // Never free the context or the pinned batch while the bridge can still use them.
      if (_worker != null && Thread.CurrentThread != _worker)
        _worker.Join();
      _worker = null;
      Cleanup();
      _onTransition = null;
    }
  }

  public void RefreshPhysicalState() { }

  public bool TryGetPhysicalKeyState(int keyCode, out bool isDown) =>
    WindowsLowLevelKeyboardEventSource.TryReadAsyncKeyState(keyCode, out isDown);

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
      return _workerFailure != null ? ReplayUnavailableReasons.InputSourceStopped : null;
  }

  public NativeInputSourceDiagnostics GetDiagnostics()
  {
    lock (_lifecycleGate)
    {
      if (_context == IntPtr.Zero)
        return _lastDiagnostics;
      WindowsNativeInputStats stats = _library.GetStats(_context);
      return new NativeInputSourceDiagnostics(Saturate(stats.CallbackCount), 0, 0, 0, (int)stats.QueueDepth);
    }
  }

  private void RunWorker()
  {
    try
    {
      IntPtr destination = _eventPin.AddrOfPinnedObject();
      WindowsPerformanceCounterConverter clock = _clock;
      while (!_stopping)
      {
        int count = _library.WaitDequeue(_context, destination, _events.Length, 100);
        for (int i = 0; i < count; i++)
        {
          WindowsNativeKeyEvent input = _events[i];
          if (
            !WindowsLowLevelKeyboardEventSource.TryCreateTransition(
              clock.ToStopwatchTicks(input.QpcTicks),
              input.TimeMs,
              input.VirtualKey,
              input.ScanCode,
              input.Flags,
              input.Down != 0,
              out NativeInputTransition transition
            )
          )
            continue;
          try
          {
            _onTransition?.Invoke(transition);
          }
          catch
          {
            Interlocked.Increment(ref _managedDropped);
          }
        }
        if (!_stopping && !_library.IsInputRunning(_context))
          throw new InvalidOperationException("Windows native keyboard hook thread stopped.");
      }
    }
    catch (Exception exception)
    {
      _workerFailure = exception;
      _running = false;
      Main.Instance?.Log("[Recording/Input] Windows native input bridge stopped. error=" + exception.Message);
    }
  }

  private void Cleanup()
  {
    if (_context != IntPtr.Zero)
    {
      _lastDiagnostics = GetDiagnostics();
      _library.DestroyInput(_context);
      _context = IntPtr.Zero;
    }
    if (_eventPin.IsAllocated)
      _eventPin.Free();
  }

  private static long Saturate(ulong value) => value >= long.MaxValue ? long.MaxValue : (long)value;
}
