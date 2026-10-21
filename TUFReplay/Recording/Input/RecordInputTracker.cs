using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Recording.Input;

public static class RecordInputTracker
{
  private const long UnixEpochTicks = 621355968000000000L;

  private static readonly object StateLock = new object();
  private static INativeInputEventSource EventSource = NativeInputEventSourceFactory.CreatePrimary();
  private static readonly NativeInputTransitionRingBuffer EventQueue = new NativeInputTransitionRingBuffer();
  private static readonly NativeInputTransition[] DrainBuffer = new NativeInputTransition[
    NativeInputTransitionRingBuffer.Capacity
  ];
  private static readonly NativeInputTransition[] FilteredDrainBuffer = new NativeInputTransition[
    NativeInputTransitionRingBuffer.Capacity
  ];
  private static readonly bool[] KeyStates = new bool[(ushort.MaxValue + 1) * 2];

  private static volatile bool _capturing;
  private static bool _captureWindowActive;
  private static volatile bool _acceptingEvents;
  private static bool _usingEvents;
  private static volatile bool _overflowed;
  private static bool _restartAttempted;
  private static string _mode = "stopped";
  private static string _fallbackReason;
  private static string _captureFailureReason;
  private static long _samples;
  private static long _received;
  private static long _transitions;
  private static long _duplicates;
  private static long _dropped;
  private static long _readFailures;
  private static long _resyncs;
  private static int _maxQueueDepth;
  private static NativeInputSourceDiagnostics _sourceDiagnostics;
  private static NativeInputSourceDiagnostics _sourceBaseline;
  private static long _captureStartTicks;
  private static long _captureWindowStartTicks;
  private static int _callbacksInFlight;

  internal static long CaptureStartTimestampTicks => _capturing ? Interlocked.Read(ref _captureStartTicks) : 0;

  public static void PrepareSource()
  {
    if (!EventSource.IsRunning)
      TryStartEventSource(EventSource);
  }

  public static void Shutdown()
  {
    Reset();
    INativeInputEventSource stopped = EventSource;
    EventSource = NativeInputEventSourceFactory.CreatePrimary();
    ThreadPool.QueueUserWorkItem(_ => StopSourceNoThrow(stopped));
  }

  public static string CaptureMode
  {
    get
    {
      lock (StateLock)
        return _mode;
    }
  }

  public static void StartCapture()
  {
    Reset();

    Exception startFailure = EventSource.IsRunning ? null : TryStartEventSource(EventSource);

    // A retained source may have faults from time spent outside recording.
    // Consume them before establishing this run's capture boundary.
    EventSource.ConsumeDroppedEvents();
    (EventSource as INativeInputCaptureHealth)?.ConsumeCaptureFailure();

    lock (StateLock)
    {
      _captureWindowActive = false;
      _captureStartTicks = Stopwatch.GetTimestamp();
      _capturing = true;
      _usingEvents = startFailure == null;
      _mode = _usingEvents ? EventSource.Name : "unsupported";
    }

    try
    {
      _sourceBaseline = EventSource.GetDiagnostics();
    }
    catch
    {
      _sourceBaseline = default;
    }

    if (startFailure != null)
    {
      _fallbackReason = GetStableFailureReason(startFailure);
      _captureFailureReason =
        startFailure is UnauthorizedAccessException
          ? ReplayUnavailableReasons.InputPermissionDenied
          : ReplayUnavailableReasons.InputStartFailed;
      Main.Instance?.Log(
        "[Recording/Input] High-resolution input recording is unsupported. source="
          + EventSource.Name
          + ", error="
          + startFailure.Message
      );
    }

    Main.Instance?.Log(
      "[Recording/InputDebug] Native capture started. mode="
        + _mode
        + ", source="
        + EventSource.Name
        + ", snapshotKeys="
        + EventSource.SnapshotKeyCodes.Count
    );
  }

  public static void StopCapture(RecordingSession session)
  {
    lock (StateLock)
    {
      _capturing = false;
      _captureWindowActive = false;
      _acceptingEvents = false;
    }

    CaptureSourceDiagnostics();
    CaptureSourceHealth();
    WaitForActiveCallback();
    int count = DrainFilteredTransitions();
    if (count > 0 && session != null)
    {
      int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(FilteredDrainBuffer, 0, count));
      Interlocked.Add(ref _transitions, recorded);
    }
  }

  public static void Reset()
  {
    _acceptingEvents = false;
    _capturing = false;
    WaitForActiveCallback();
    lock (StateLock)
    {
      _capturing = false;
      _captureWindowActive = false;
      _acceptingEvents = false;
      _usingEvents = false;
      _overflowed = false;
      _restartAttempted = false;
      _mode = "stopped";
      _fallbackReason = null;
      _captureFailureReason = null;
      // Clear only the consumer cursor: an already-entered producer must never
      // race a reset of its write cursor when the hook is retained between runs.
      EventQueue.Clear();
      Array.Clear(KeyStates, 0, KeyStates.Length);
      _samples = 0;
      _received = 0;
      _transitions = 0;
      _duplicates = 0;
      _dropped = 0;
      _readFailures = 0;
      _resyncs = 0;
      _maxQueueDepth = 0;
      _sourceDiagnostics = default;
      _captureWindowStartTicks = 0;
    }
  }

  public static void SetCaptureWindowActive(bool active)
  {
    lock (StateLock)
    {
      if (!_capturing || !_usingEvents || _captureWindowActive == active)
        return;

      _acceptingEvents = false;
      _captureWindowActive = active;

      if (active)
      {
        _captureWindowStartTicks = Stopwatch.GetTimestamp();
        WaitForActiveCallback();
        EventQueue.Clear();
        SynchronizePhysicalStateLocked(emitTransitions: true);
        _acceptingEvents = !_overflowed;
        Interlocked.Increment(ref _resyncs);
      }
    }
  }

  public static void DrainCapturedTransitions(RecordingSession session)
  {
    if (!_capturing || !_usingEvents)
      return;
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;

    Interlocked.Increment(ref _samples);

    CaptureSourceHealth();

    int count = DrainFilteredTransitions();
    if (count > 0)
    {
      int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(FilteredDrainBuffer, 0, count));
      Interlocked.Add(ref _transitions, recorded);
    }
    if (!EnsureEventSourceRunning())
      return;
    RecoverFromOverflow();
  }

  public static string DebugSnapshot()
  {
    string mode;
    bool capturing;
    bool captureWindowActive;
    bool usingEvents;
    lock (StateLock)
    {
      mode = _mode;
      capturing = _capturing;
      captureWindowActive = _captureWindowActive;
      usingEvents = _usingEvents;
    }

    return "capturing="
      + capturing
      + ", captureWindowActive="
      + captureWindowActive
      + ", mode="
      + mode
      + ", source="
      + EventSource.Name
      + ", snapshotKeys="
      + EventSource.SnapshotKeyCodes.Count
      + ", samples="
      + Interlocked.Read(ref _samples)
      + ", received="
      + Interlocked.Read(ref _received)
      + ", transitions="
      + Interlocked.Read(ref _transitions)
      + ", duplicates="
      + Interlocked.Read(ref _duplicates)
      + ", dropped="
      + Interlocked.Read(ref _dropped)
      + ", maxQueueDepth="
      + Volatile.Read(ref _maxQueueDepth)
      + ", resyncs="
      + Interlocked.Read(ref _resyncs)
      + ", readFailures="
      + Interlocked.Read(ref _readFailures);
  }

  public static void CopyDiagnosticsTo(RecordedRunPayload payload)
  {
    if (payload == null)
      return;

    lock (StateLock)
    {
      payload.InputCapture = _mode;
      payload.InputFallbackReason = _fallbackReason;
      payload.InputFailureReason = _captureFailureReason;
    }
    payload.InputReceived = Interlocked.Read(ref _received);
    payload.InputRecorded = payload.Inputs?.Count ?? 0;
    payload.InputRepeatDropped = Interlocked.Read(ref _duplicates);
    payload.InputOverflowDropped = Interlocked.Read(ref _dropped);
    payload.InputResyncs = Interlocked.Read(ref _resyncs);
    payload.InputReadFailures = Interlocked.Read(ref _readFailures);
    payload.InputMaxQueueDepth = Volatile.Read(ref _maxQueueDepth);
    payload.InputNativeCallbacks = _sourceDiagnostics.Callbacks;
    payload.InputNativeRepeatDropped = _sourceDiagnostics.Repeats;
    payload.InputNativeUnmapped = _sourceDiagnostics.Unmapped;
    payload.InputNativeDevices = _sourceDiagnostics.Devices;
    payload.InputNativeQueueDepth = _sourceDiagnostics.QueueDepth;
  }

  private static void OnNativeTransition(NativeInputTransition transition)
  {
    Interlocked.Increment(ref _callbacksInFlight);
    try
    {
      CaptureNativeTransition(transition);
    }
    finally
    {
      Interlocked.Decrement(ref _callbacksInFlight);
    }
  }

  private static void CaptureNativeTransition(NativeInputTransition transition)
  {
    if (!_capturing)
      return;

    Interlocked.Increment(ref _received);

    if (_overflowed)
    {
      Interlocked.Increment(ref _dropped);
      return;
    }
    if (!_acceptingEvents)
      return;
    if (!TryGetStateIndex(transition.Key, transition.ExtendedKey, out _))
    {
      Interlocked.Increment(ref _readFailures);
      return;
    }
    if (transition.CaptureTimestampTicks < Volatile.Read(ref _captureWindowStartTicks))
      return;

    // This method runs directly on the platform hook/event-tap thread. Never
    // wait for Unity's state lock here: a blocked macOS event-tap callback can
    // back up system-wide input delivery. State transitions and repeat removal
    // are applied by the Unity thread when it drains this SPSC queue.
    if (!EventQueue.TryEnqueue(transition))
    {
      _overflowed = true;
      _acceptingEvents = false;
      Interlocked.Increment(ref _dropped);
      return;
    }

    UpdateMaxQueueDepth(EventQueue.Count);
  }

  private static int DrainFilteredTransitions()
  {
    int count = EventQueue.DrainTo(DrainBuffer);
    int accepted = 0;
    for (int i = 0; i < count; i++)
    {
      NativeInputTransition transition = DrainBuffer[i];
      if (transition.CaptureTimestampTicks < Math.Max(_captureStartTicks, _captureWindowStartTicks))
        continue;
      if (!TryGetStateIndex(transition.Key, transition.ExtendedKey, out int stateIndex))
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }
      if (KeyStates[stateIndex] == transition.Down)
      {
        Interlocked.Increment(ref _duplicates);
        continue;
      }

      KeyStates[stateIndex] = transition.Down;
      FilteredDrainBuffer[accepted++] = transition;
    }
    return accepted;
  }

  private static void SynchronizePhysicalStateLocked(bool emitTransitions)
  {
    EventSource.RefreshPhysicalState();
    long timestampNs = CurrentUnixTimeNs();
    long captureTicks = Stopwatch.GetTimestamp();
    IReadOnlyList<int> keyCodes = EventSource.SnapshotKeyCodes;
    for (int i = 0; i < keyCodes.Count; i++)
    {
      int key = keyCodes[i];
      bool extendedKey = EventSource.UsesExtendedKeyState && WindowsNativeInputKey.IsExtended(key);
      if (
        !TryGetStateIndex(key, extendedKey, out int stateIndex)
        || !EventSource.TryGetPhysicalKeyState(key, out bool isDown)
      )
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }

      bool wasDown = KeyStates[stateIndex];
      if (!emitTransitions)
        KeyStates[stateIndex] = isDown;
      if (!emitTransitions || isDown == wasDown)
        continue;

      if (!EventQueue.TryEnqueue(new NativeInputTransition(captureTicks, timestampNs, key, isDown, extendedKey)))
      {
        _overflowed = true;
        _acceptingEvents = false;
        Interlocked.Increment(ref _dropped);
        return;
      }

      UpdateMaxQueueDepth(EventQueue.Count);
    }
  }

  private static bool EnsureEventSourceRunning()
  {
    try
    {
      if (EventSource.IsRunning)
        return true;
    }
    catch (Exception exception)
    {
      SwitchToUnsupported(exception);
      return false;
    }

    if (!_restartAttempted)
    {
      MarkCaptureFailure(ReplayUnavailableReasons.InputSourceStopped);
      _restartAttempted = true;
      try
      {
        lock (StateLock)
        {
          _acceptingEvents = false;
        }

        EventSource.Stop();
        EventSource.Start(OnNativeTransition);
        if (EventSource.IsRunning)
        {
          lock (StateLock)
          {
            SynchronizePhysicalStateLocked(emitTransitions: true);
            _acceptingEvents = _captureWindowActive && !_overflowed;
            Interlocked.Increment(ref _resyncs);
          }

          Main.Instance?.Log("[Recording/Input] Native capture source restarted. source=" + EventSource.Name);
          return true;
        }
      }
      catch (Exception exception)
      {
        SwitchToUnsupported(exception);
        return false;
      }
    }

    SwitchToUnsupported(new InvalidOperationException(EventSource.Name + " stopped while input capture was active."));
    return false;
  }

  private static void SwitchToUnsupported(Exception exception)
  {
    lock (StateLock)
    {
      _acceptingEvents = false;
      _usingEvents = false;
      _overflowed = false;
      _mode = "unsupported";
      _fallbackReason = GetStableFailureReason(exception);
      if (_captureFailureReason == null)
        _captureFailureReason = ReplayUnavailableReasons.InputSourceStopped;
    }

    StopEventSourceNoThrow();
    Main.Instance?.Log("[Recording/Input] Native capture stopped; no fallback is enabled. error=" + exception.Message);
  }

  private static string GetStableFailureReason(Exception exception)
  {
    string macOsReason = MacOsInputMonitoringAccess.FailureReason;
    return !string.IsNullOrEmpty(macOsReason) ? macOsReason : EventSource.Name + ": " + exception.Message;
  }

  private static void RecoverFromOverflow()
  {
    lock (StateLock)
    {
      if (!_overflowed)
        return;

      _acceptingEvents = false;
      WaitForActiveCallback();
      EventQueue.Clear();
      SynchronizePhysicalStateLocked(emitTransitions: false);
      _overflowed = false;
      _acceptingEvents = _capturing && _captureWindowActive;
      Interlocked.Increment(ref _resyncs);
    }

    Main.Instance?.Log(
      "[Recording/Input] Event buffer overflowed; physical state resynchronized once. dropped="
        + Interlocked.Read(ref _dropped)
    );
  }

  private static void StopEventSourceNoThrow()
  {
    INativeInputEventSource source = EventSource;
    ThreadPool.QueueUserWorkItem(_ => StopSourceNoThrow(source));
  }

  private static void StopSourceNoThrow(INativeInputEventSource source)
  {
    try
    {
      source.Stop();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Recording/Input] Failed to stop native source cleanly. error=" + exception.Message);
    }
  }

  private static void CaptureSourceDiagnostics()
  {
    try
    {
      NativeInputSourceDiagnostics current = EventSource.GetDiagnostics();
      _sourceDiagnostics = new NativeInputSourceDiagnostics(
        Math.Max(0L, current.Callbacks - _sourceBaseline.Callbacks),
        Math.Max(0L, current.Repeats - _sourceBaseline.Repeats),
        Math.Max(0L, current.Unmapped - _sourceBaseline.Unmapped),
        current.Devices,
        current.QueueDepth
      );
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Recording/Input] Failed to read native source diagnostics. error=" + exception.Message);
    }
  }

  private static void CaptureSourceHealth()
  {
    try
    {
      string failure = (EventSource as INativeInputCaptureHealth)?.ConsumeCaptureFailure();
      if (failure != null)
        MarkCaptureFailure(failure);
      long dropped = EventSource.ConsumeDroppedEvents();
      if (dropped > 0)
      {
        Interlocked.Add(ref _dropped, dropped);
        _overflowed = true;
        _acceptingEvents = false;
        MarkCaptureFailure(ReplayUnavailableReasons.InputQueueOverflow);
      }
      if (!EventSource.IsRunning && _usingEvents)
        MarkCaptureFailure(ReplayUnavailableReasons.InputSourceStopped);
    }
    catch (Exception exception)
    {
      MarkCaptureFailure(ReplayUnavailableReasons.InputReadFailed);
      Main.Instance?.Log("[Recording/Input] Could not inspect capture health: " + exception.Message);
    }
  }

  private static void MarkCaptureFailure(string reason)
  {
    lock (StateLock)
    {
      if (_captureFailureReason != null)
        return;
      _captureFailureReason = reason;
    }
    Main.Instance?.Log("[Recording/Input] This run will be saved without playable replay. reason=" + reason);
  }

  private static Exception TryStartEventSource(INativeInputEventSource source)
  {
    try
    {
      source.Start(transition =>
      {
        if (ReferenceEquals(EventSource, source))
          OnNativeTransition(transition);
      });
      if (!source.IsRunning)
        throw new InvalidOperationException(source.Name + " did not report a running hook after startup.");
      return null;
    }
    catch (Exception exception)
    {
      try
      {
        source.Stop();
      }
      catch
      {
        // Preserve the original startup failure.
      }
      return exception;
    }
  }

  private static long CurrentUnixTimeNs()
  {
    return (DateTime.UtcNow.Ticks - UnixEpochTicks) * 100L;
  }

  private static void WaitForActiveCallback()
  {
    // Callbacks only enqueue a struct; never wait for the OS hook or bridge
    // thread to terminate. Bound the rare in-flight handoff to one millisecond.
    long deadline = Stopwatch.GetTimestamp() + Math.Max(1L, Stopwatch.Frequency / 1000);
    while (Volatile.Read(ref _callbacksInFlight) != 0 && Stopwatch.GetTimestamp() < deadline)
      Thread.SpinWait(16);
  }

  private static bool TryGetStateIndex(int key, bool extendedKey, out int stateIndex)
  {
    stateIndex = 0;
    if (key < 0 || key > ushort.MaxValue)
      return false;

    stateIndex = key + (extendedKey ? ushort.MaxValue + 1 : 0);
    return true;
  }

  private static void UpdateMaxQueueDepth(int depth)
  {
    int current = Volatile.Read(ref _maxQueueDepth);
    while (depth > current)
    {
      int observed = Interlocked.CompareExchange(ref _maxQueueDepth, depth, current);
      if (observed == current)
        return;
      current = observed;
    }
  }
}
