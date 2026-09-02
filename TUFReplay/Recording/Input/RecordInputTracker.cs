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
  private static long _samples;
  private static long _received;
  private static long _transitions;
  private static long _duplicates;
  private static long _dropped;
  private static long _readFailures;
  private static long _resyncs;
  private static int _maxQueueDepth;
  private static NativeInputSourceDiagnostics _sourceDiagnostics;

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

    lock (StateLock)
      _capturing = true;

    Exception startFailure = TryStartEventSource(EventSource);

    lock (StateLock)
    {
      _captureWindowActive = false;
      _usingEvents = startFailure == null;
      _mode = _usingEvents ? EventSource.Name : "unsupported";
    }

    if (startFailure != null)
    {
      _fallbackReason = GetStableFailureReason(startFailure);
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
    StopEventSourceNoThrow();
    int count = DrainFilteredTransitions();
    if (count > 0 && session != null)
    {
      int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(FilteredDrainBuffer, 0, count));
      Interlocked.Add(ref _transitions, recorded);
    }
    EventSource = NativeInputEventSourceFactory.CreatePrimary();
  }

  public static void Reset()
  {
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
      EventQueue.Reset();
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
    }

    StopEventSourceNoThrow();
    EventSource = NativeInputEventSourceFactory.CreatePrimary();
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

    long sourceDropped = EventSource.ConsumeDroppedEvents();
    if (sourceDropped > 0)
    {
      Interlocked.Add(ref _dropped, sourceDropped);
      _overflowed = true;
      _acceptingEvents = false;
    }

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
    try
    {
      EventSource.Stop();
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
      _sourceDiagnostics = EventSource.GetDiagnostics();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Recording/Input] Failed to read native source diagnostics. error=" + exception.Message);
    }
  }

  private static Exception TryStartEventSource(INativeInputEventSource source)
  {
    try
    {
      source.Start(OnNativeTransition);
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
