using System;
using System.Collections.Generic;
using System.Threading;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.NativeInput.Capture;

namespace TUFReplay.Application.Recording;

public static class RecordInputTracker
{
  private const long UnixEpochTicks = 621355968000000000L;

  private static readonly object StateLock = new object();
  private static readonly INativeInputStateReader StateReader = NativeInputStateReaderFactory.Create();
  private static readonly INativeInputEventSource EventSource = new SkyHookNativeInputEventSource();
  private static readonly NativeInputTransitionRingBuffer EventQueue = new NativeInputTransitionRingBuffer();
  private static readonly NativeInputTransition[] DrainBuffer = new NativeInputTransition[
    NativeInputTransitionRingBuffer.Capacity
  ];
  private static readonly bool[] KeyStates = new bool[ushort.MaxValue + 1];

  private static bool _capturing;
  private static bool _captureWindowActive;
  private static bool _acceptingEvents;
  private static bool _usingEvents;
  private static bool _overflowed;
  private static bool _restartAttempted;
  private static string _mode = "stopped";
  private static long _samples;
  private static long _received;
  private static long _transitions;
  private static long _duplicates;
  private static long _dropped;
  private static long _readFailures;
  private static long _resyncs;
  private static int _maxQueueDepth;

  public static string CaptureMode
  {
    get
    {
      lock (StateLock)
        return _usingEvents ? "skyhook-events-high-resolution" : "native-state-polling-low-resolution";
    }
  }

  public static void StartCapture()
  {
    Reset();

    Exception startFailure = null;
    try
    {
      EventSource.Start(OnNativeTransition);
      if (!EventSource.IsRunning)
        throw new InvalidOperationException("SkyHook did not report a running hook after startup.");
    }
    catch (Exception exception)
    {
      startFailure = exception;
      StopEventSourceNoThrow();
    }

    lock (StateLock)
    {
      _capturing = true;
      _captureWindowActive = false;
      _usingEvents = startFailure == null;
      _mode = _usingEvents ? "skyhook-events" : "native-state-sample-fallback";
    }

    if (startFailure != null)
    {
      Main.Instance?.Log(
        "[Recording/Input] LOW_RESOLUTION fallback: SkyHook unavailable; using state polling. error="
          + startFailure.Message
      );
    }

    Main.Instance?.Log(
      "[Recording/InputDebug] Native capture started. mode="
        + _mode
        + ", source="
        + (_usingEvents ? EventSource.Name : StateReader.Name)
        + ", supportedKeys="
        + StateReader.KeyCodes.Count
    );
  }

  public static void StopCapture()
  {
    lock (StateLock)
    {
      _capturing = false;
      _captureWindowActive = false;
      _acceptingEvents = false;
      EventQueue.Clear();
    }

    StopEventSourceNoThrow();
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
    }

    StopEventSourceNoThrow();
  }

  public static void SetCaptureWindowActive(bool active)
  {
    lock (StateLock)
    {
      if (!_capturing || !_usingEvents || _captureWindowActive == active)
        return;

      _acceptingEvents = false;
      EventQueue.Clear();
      _captureWindowActive = active;

      if (active)
      {
        SynchronizeStatesLocked(emitTransitions: true);
        _acceptingEvents = !_overflowed;
        Interlocked.Increment(ref _resyncs);
      }
    }
  }

  public static void Sample(RecordingSession session)
  {
    if (!_capturing)
      return;
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;

    Interlocked.Increment(ref _samples);

    if (_usingEvents)
    {
      if (!EnsureEventSourceRunning())
      {
        SamplePolling(session);
        return;
      }

      int count = EventQueue.DrainTo(DrainBuffer);
      if (count > 0)
      {
        int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(DrainBuffer, 0, count));
        Interlocked.Add(ref _transitions, recorded);
      }

      RecoverFromOverflow();
      return;
    }

    SamplePolling(session);
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
      + (usingEvents ? EventSource.Name : StateReader.Name)
      + ", supportedKeys="
      + StateReader.KeyCodes.Count
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

  private static void OnNativeTransition(NativeInputTransition transition)
  {
    lock (StateLock)
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
      if (transition.Key < 0 || transition.Key >= KeyStates.Length)
      {
        Interlocked.Increment(ref _readFailures);
        return;
      }
      if (KeyStates[transition.Key] == transition.Down)
      {
        Interlocked.Increment(ref _duplicates);
        return;
      }

      if (!EventQueue.TryEnqueue(transition))
      {
        _overflowed = true;
        _acceptingEvents = false;
        Interlocked.Increment(ref _dropped);
        return;
      }

      KeyStates[transition.Key] = transition.Down;
      UpdateMaxQueueDepth(EventQueue.Count);
    }
  }

  private static void SamplePolling(RecordingSession session)
  {
    try
    {
      StateReader.Refresh();
    }
    catch
    {
      Interlocked.Increment(ref _readFailures);
      return;
    }

    IReadOnlyList<int> keyCodes = StateReader.KeyCodes;
    for (int i = 0; i < keyCodes.Count; i++)
    {
      int key = keyCodes[i];
      if (key < 0 || key >= KeyStates.Length || !StateReader.TryGetIsDown(key, out bool isDown))
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }

      bool wasDown = KeyStates[key];
      if (isDown == wasDown)
        continue;

      KeyStates[key] = isDown;
      RecordInputFlags flags = RecordInputFlags.Async;
      if (isDown)
        flags |= RecordInputFlags.Down;

      session.AddInputAtCurrentTime(key, flags);
      Interlocked.Increment(ref _transitions);
    }
  }

  private static void SynchronizeStatesLocked(bool emitTransitions)
  {
    try
    {
      StateReader.Refresh();
    }
    catch
    {
      Interlocked.Increment(ref _readFailures);
      return;
    }

    long timestampNs = CurrentUnixTimeNs();
    IReadOnlyList<int> keyCodes = StateReader.KeyCodes;
    for (int i = 0; i < keyCodes.Count; i++)
    {
      int key = keyCodes[i];
      if (key < 0 || key >= KeyStates.Length || !StateReader.TryGetIsDown(key, out bool isDown))
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }

      bool wasDown = KeyStates[key];
      KeyStates[key] = isDown;
      if (!emitTransitions || isDown == wasDown)
        continue;

      if (!EventQueue.TryEnqueue(new NativeInputTransition(timestampNs, key, isDown)))
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
      SwitchToPollingFallback(exception);
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
            EventQueue.Clear();
            SynchronizeStatesLocked(emitTransitions: true);
            _acceptingEvents = _captureWindowActive && !_overflowed;
            Interlocked.Increment(ref _resyncs);
          }

          Main.Instance?.Log("[Recording/Input] SkyHook restarted after an unexpected stop.");
          return true;
        }
      }
      catch (Exception exception)
      {
        SwitchToPollingFallback(exception);
        return false;
      }
    }

    SwitchToPollingFallback(new InvalidOperationException("SkyHook stopped while input capture was active."));
    return false;
  }

  private static void SwitchToPollingFallback(Exception exception)
  {
    lock (StateLock)
    {
      _acceptingEvents = false;
      _usingEvents = false;
      _overflowed = false;
      _mode = "native-state-sample-fallback";
      EventQueue.Clear();
    }

    StopEventSourceNoThrow();
    Main.Instance?.Log(
      "[Recording/Input] LOW_RESOLUTION fallback: SkyHook stopped; using state polling. error=" + exception.Message
    );
  }

  private static void RecoverFromOverflow()
  {
    lock (StateLock)
    {
      if (!_overflowed)
        return;

      _acceptingEvents = false;
      EventQueue.Clear();
      SynchronizeStatesLocked(emitTransitions: false);
      _overflowed = false;
      _acceptingEvents = _capturing && _captureWindowActive;
      Interlocked.Increment(ref _resyncs);
    }

    Main.Instance?.Log(
      "[Recording/Input] Event buffer overflowed; input state resynchronized. dropped=" + Interlocked.Read(ref _dropped)
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
      Main.Instance?.Log("[Recording/Input] Failed to stop SkyHook cleanly. error=" + exception.Message);
    }
  }

  private static long CurrentUnixTimeNs()
  {
    return (DateTime.UtcNow.Ticks - UnixEpochTicks) * 100L;
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
