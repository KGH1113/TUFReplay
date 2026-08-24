using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
  private static readonly INativeInputStateReader StateReader = NativeInputStateReaderFactory.Create();
  private static INativeInputEventSource EventSource = NativeInputEventSourceFactory.CreatePrimary();
  private static readonly INativeInputEventSource FallbackEventSource = NativeInputEventSourceFactory.CreateFallback();
  private static readonly NativeInputTransitionRingBuffer EventQueue = new NativeInputTransitionRingBuffer();
  private static readonly NativeInputTransition[] DrainBuffer = new NativeInputTransition[
    NativeInputTransitionRingBuffer.Capacity
  ];
  private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
  private static readonly bool[] KeyStates = new bool[(ushort.MaxValue + 1) * 2];

  private static bool _capturing;
  private static bool _captureWindowActive;
  private static bool _acceptingEvents;
  private static bool _usingEvents;
  private static bool _overflowed;
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

  public static string CaptureMode
  {
    get
    {
      lock (StateLock)
        return _usingEvents ? _mode : "native-state-polling-low-resolution";
    }
  }

  public static void StartCapture()
  {
    Reset();

    Exception startFailure = TryStartEventSource(EventSource);
    if (startFailure != null && !ReferenceEquals(EventSource, FallbackEventSource))
    {
      string failedSource = EventSource.Name;
      Main.Instance?.Log(
        "[Recording/Input] Native source unavailable; trying SkyHook. source="
          + EventSource.Name
          + ", error="
          + startFailure.Message
      );
      EventSource = FallbackEventSource;
      Exception fallbackFailure = TryStartEventSource(EventSource);
      if (fallbackFailure == null)
        _fallbackReason = failedSource + ": " + startFailure.Message;
      startFailure = fallbackFailure;
    }

    lock (StateLock)
    {
      _capturing = true;
      _captureWindowActive = false;
      _usingEvents = startFailure == null;
      _mode = _usingEvents ? EventSource.Name : "native-state-sample-fallback";
    }

    if (startFailure != null)
    {
      _fallbackReason = EventSource.Name + ": " + startFailure.Message;
      Main.Instance?.Log(
        "[Recording/Input] LOW_RESOLUTION fallback: event sources unavailable; using state polling. error="
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

  public static void StopCapture(RecordingSession session)
  {
    lock (StateLock)
    {
      _capturing = false;
      _captureWindowActive = false;
      _acceptingEvents = false;
    }

    int count = EventQueue.DrainTo(DrainBuffer);
    if (count > 0 && session != null)
    {
      int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(DrainBuffer, 0, count));
      Interlocked.Add(ref _transitions, recorded);
    }
    StopEventSourceNoThrow();
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
      int count = EventQueue.DrainTo(DrainBuffer);
      if (count > 0)
      {
        int recorded = session.AddInputBatch(new ReadOnlySpan<NativeInputTransition>(DrainBuffer, 0, count));
        Interlocked.Add(ref _transitions, recorded);
      }

      if (!EnsureEventSourceRunning())
      {
        SamplePolling(session);
        return;
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

  public static void CopyDiagnosticsTo(RecordedRunPayload payload)
  {
    if (payload == null)
      return;

    lock (StateLock)
    {
      payload.InputCapture = _usingEvents ? _mode : "native-state-polling-low-resolution";
      payload.InputFallbackReason = _fallbackReason;
    }
    payload.InputReceived = Interlocked.Read(ref _received);
    payload.InputRecorded = payload.Inputs?.Count ?? 0;
    payload.InputRepeatDropped = Interlocked.Read(ref _duplicates);
    payload.InputOverflowDropped = Interlocked.Read(ref _dropped);
    payload.InputResyncs = Interlocked.Read(ref _resyncs);
    payload.InputReadFailures = Interlocked.Read(ref _readFailures);
    payload.InputMaxQueueDepth = Volatile.Read(ref _maxQueueDepth);
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
      if (!TryGetStateIndex(transition.Key, transition.ExtendedKey, out int stateIndex))
      {
        Interlocked.Increment(ref _readFailures);
        return;
      }
      if (KeyStates[stateIndex] == transition.Down)
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

      KeyStates[stateIndex] = transition.Down;
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
      bool extendedKey = IsWindows && WindowsNativeInputKey.IsExtended(key);
      if (!TryGetStateIndex(key, extendedKey, out int stateIndex) || !StateReader.TryGetIsDown(key, out bool isDown))
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }

      bool wasDown = KeyStates[stateIndex];
      if (isDown == wasDown)
        continue;

      KeyStates[stateIndex] = isDown;
      RecordInputFlags flags = RecordInputFlags.Async;
      if (isDown)
        flags |= RecordInputFlags.Down;
      if (extendedKey)
        flags |= RecordInputFlags.ExtendedKey;

      session.AddInputAtCurrentTime(key, flags);
      Interlocked.Increment(ref _transitions);
    }
  }

  private static void SynchronizeStatesLocked(bool emitTransitions)
  {
    try
    {
      StateReader.RefreshPhysicalState();
    }
    catch
    {
      Interlocked.Increment(ref _readFailures);
      return;
    }

    long timestampNs = CurrentUnixTimeNs();
    long captureTicks = Stopwatch.GetTimestamp();
    IReadOnlyList<int> keyCodes = StateReader.KeyCodes;
    for (int i = 0; i < keyCodes.Count; i++)
    {
      int key = keyCodes[i];
      bool extendedKey = IsWindows && WindowsNativeInputKey.IsExtended(key);
      if (!TryGetStateIndex(key, extendedKey, out int stateIndex) || !StateReader.TryGetIsDown(key, out bool isDown))
      {
        Interlocked.Increment(ref _readFailures);
        continue;
      }

      bool wasDown = KeyStates[stateIndex];
      KeyStates[stateIndex] = isDown;
      if (!emitTransitions || isDown == wasDown)
        continue;

      if (
        !EventQueue.TryEnqueue(
          new NativeInputTransition(captureTicks, timestampNs, key, isDown, extendedKey)
        )
      )
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
      return TrySwitchToFallbackEventSource(exception);
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
            SynchronizeStatesLocked(emitTransitions: true);
            _acceptingEvents = _captureWindowActive && !_overflowed;
            Interlocked.Increment(ref _resyncs);
          }

          Main.Instance?.Log("[Recording/Input] Capture source restarted after an unexpected stop. source=" + EventSource.Name);
          return true;
        }
      }
      catch (Exception exception)
      {
        return TrySwitchToFallbackEventSource(exception);
      }
    }

    return TrySwitchToFallbackEventSource(
      new InvalidOperationException(EventSource.Name + " stopped while input capture was active.")
    );
  }

  private static bool TrySwitchToFallbackEventSource(Exception exception)
  {
    if (!ReferenceEquals(EventSource, FallbackEventSource))
    {
      string failedSource = EventSource.Name;
      lock (StateLock)
        _acceptingEvents = false;
      StopEventSourceNoThrow();
      EventSource = FallbackEventSource;
      Exception fallbackFailure = TryStartEventSource(EventSource);
      if (fallbackFailure == null)
      {
        lock (StateLock)
        {
          _usingEvents = true;
          _overflowed = false;
          _restartAttempted = false;
          _mode = EventSource.Name;
          _fallbackReason = failedSource + ": " + exception.Message;
          SynchronizeStatesLocked(emitTransitions: true);
          _acceptingEvents = _capturing && _captureWindowActive && !_overflowed;
          Interlocked.Increment(ref _resyncs);
        }
        Main.Instance?.Log(
          "[Recording/Input] Capture source failed; switched to SkyHook. source="
            + failedSource
            + ", error="
            + exception.Message
        );
        return true;
      }

      exception = new AggregateException(exception, fallbackFailure);
    }

    SwitchToPollingFallback(exception);
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
      _fallbackReason = EventSource.Name + ": " + exception.Message;
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
