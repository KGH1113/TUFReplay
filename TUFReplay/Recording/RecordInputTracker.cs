using System;
using System.Collections.Concurrent;
using System.Threading;
using SkyHook;

namespace TUFReplay.Recording;

public static class RecordInputTracker
{
  private static readonly ConcurrentQueue<PendingRecordInput> PendingInputs = new ConcurrentQueue<PendingRecordInput>();

  private static long _gameplayStartTicks;
  private static volatile bool _capturing;

  private static long _rawSeen;
  private static long _rawIgnoredNotCapturing;
  private static long _rawIgnoredNonKey;
  private static long _rawEnqueued;
  private static long _drained;

  public static void StartCapture()
  {
    Reset();
    _capturing = true;
  }

  public static void StopCapture()
  {
    _capturing = false;
  }

  public static void EnqueueRawAsync(SkyHookEvent ev)
  {
    Interlocked.Increment(ref _rawSeen);

    if (!_capturing)
    {
      Interlocked.Increment(ref _rawIgnoredNotCapturing);
      return;
    }

    bool down;
    if (ev.Type == EventType.KeyPressed)
    {
      down = true;
    }
    else if (ev.Type == EventType.KeyReleased)
    {
      down = false;
    }
    else
    {
      Interlocked.Increment(ref _rawIgnoredNonKey);
      return;
    }

    RecordInputFlags flags = RecordInputFlags.Async;
    if (down) flags |= RecordInputFlags.Down;

    Enqueue(ev.GetTimeInTicks(), (int)ev.Label, flags);
    Interlocked.Increment(ref _rawEnqueued);
  }

  public static void MarkGameplayStarted()
  {
    if (Interlocked.Read(ref _gameplayStartTicks) != 0L) return;
    Interlocked.Exchange(ref _gameplayStartTicks, DateTime.Now.Ticks);
  }

  public static void Reset()
  {
    _capturing = false;
    Interlocked.Exchange(ref _gameplayStartTicks, 0L);

    while (PendingInputs.TryDequeue(out _))
    {
    }
  }

  public static void Enqueue(long timeUs, int key, RecordInputFlags flags)
  {
    PendingInputs.Enqueue(new PendingRecordInput(timeUs, key, flags));
  }

  public static void DrainTo(RecordingSession session)
  {
    if (session == null) return;

    long gameplayStartTicks = Interlocked.Read(ref _gameplayStartTicks);
    if (gameplayStartTicks == 0L) return;

    int drainedNow = 0;
    while (PendingInputs.TryDequeue(out PendingRecordInput input))
    {
      long timeUs = (input.EventTicks - gameplayStartTicks) / 10L;
      session.AddInput(timeUs, input.Key, input.Flags);
      drainedNow++;
    }

    if (drainedNow > 0)
    {
      Interlocked.Add(ref _drained, drainedNow);
    }
  }

  public static string DebugSnapshot()
  {
    return
      "capturing=" + _capturing +
      ", gameplayStartTicks=" + Interlocked.Read(ref _gameplayStartTicks) +
      ", pending=" + PendingInputs.Count +
      ", rawSeen=" + Interlocked.Read(ref _rawSeen) +
      ", ignoredNotCapturing=" + Interlocked.Read(ref _rawIgnoredNotCapturing) +
      ", ignoredNonKey=" + Interlocked.Read(ref _rawIgnoredNonKey) +
      ", enqueued=" + Interlocked.Read(ref _rawEnqueued) +
      ", drained=" + Interlocked.Read(ref _drained);
  }

  private readonly struct PendingRecordInput
  {
    public readonly long EventTicks;
    public readonly int Key;
    public readonly RecordInputFlags Flags;

    public PendingRecordInput(long eventTicks, int key, RecordInputFlags flags)
    {
      EventTicks = eventTicks;
      Key = key;
      Flags = flags;
    }
  }
}
