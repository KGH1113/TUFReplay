using TUFReplay.Recording.Models;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using static TestFixture;

internal static class ReplayNativeInputConcurrencySuite
{
  internal static void RunAll()
  {
    TestControlsReturnWhileNativeEmissionIsBlocked();
    TestSeekCancelsPartialRetryAndOlderSeek();
    TestPauseCancelsOverdueInput();
    TestFocusResumePreservesReleaseBoundary();
    TestHeartbeatUsesPublicationTime();
    TestReplacementWaitsForPreviousRelease();
  }

  private static RecordedInput Input(long timeUs, int key, bool down) =>
    new RecordedInput(timeUs, key, RecordInputFlags.Async | (down ? RecordInputFlags.Down : 0));

  private static List<RecordedInput> Chord() =>
    new List<RecordedInput>
    {
      Input(0, 61, true),
      Input(0, 62, true),
      Input(10_000, 61, false),
      Input(10_000, 62, false),
      Input(20_000, 63, true),
      Input(40_000, 63, false),
      Input(50_000, 64, true),
      Input(10_000_000, 64, false),
    };

  private static void TestControlsReturnWhileNativeEmissionIsBlocked()
  {
    using var emitter = new BlockingEmitter();
    var waiter = new TrackingWaiter();
    var pump = new ReplayNativeInputPump(new ReplayInputScheduler(Chord()), emitter, new ManualClock(), waiter);
    try
    {
      // ResetTo must also return when restoring a held chord calls the emitter.
      AssertResponsive(() => pump.ResetTo(0, 1, true));
      Assert(emitter.Entered.Wait(2000), "The native emission did not reach the simulated Windows hook.");
      AssertResponsive(() =>
      {
        for (int i = 0; i < 10_000; i++)
          pump.Synchronize(i, 1, true);
        _ = pump.Snapshot;
        _ = pump.Finished;
        pump.SuspendAt(100);
        pump.ResetTo(0, 1, true);
        pump.Reset();
        pump.ReleaseAll();
        pump.Dispose();
        pump.Dispose();
      });
      Assert(!waiter.Disposed.IsSet, "Dispose destroyed a worker resource while native emission was in flight.");
      emitter.Continue.Set();
      Assert(waiter.Disposed.Wait(2000), "The worker did not finish asynchronous cleanup.");
      AssertOnlyPressAndRelease(emitter.Events(), "Shutdown retried a stale chord or left an accepted key held.");
      AssertResponsive(() =>
      {
        pump.Synchronize(0, 1, true);
        pump.Reset();
        pump.Dispose();
        _ = pump.Snapshot;
      });
    }
    finally
    {
      emitter.Continue.Set();
      pump.Dispose();
      Assert(waiter.Disposed.Wait(2000), "Shutdown cleanup did not complete.");
    }
  }

  private static void TestSeekCancelsPartialRetryAndOlderSeek()
  {
    using var emitter = new BlockingEmitter();
    var waiter = new TrackingWaiter();
    var pump = new ReplayNativeInputPump(new ReplayInputScheduler(Chord()), emitter, new ManualClock(), waiter);
    try
    {
      pump.ResetTo(0, 1, true);
      Assert(emitter.Entered.Wait(2000), "Seek test never reached native emission.");
      AssertResponsive(() =>
      {
        pump.ResetTo(30_000, 1, true);
        pump.ResetTo(60_000, 1, true);
      });
      emitter.Continue.Set();
      Assert(SpinWait.SpinUntil(() => pump.Snapshot.StateSeeks == 3, 2000), "Queued seeks did not complete.");
      NativeInputEmission[] events = emitter.Events();
      Assert(events.Length == 3, "Seek emitted a stale chord tail or an obsolete seek's held keys.");
      Assert(events[0].Key == 61 && events[0].Down, "Accepted prefix was lost.");
      Assert(events[1].Key == 61 && !events[1].Down, "Seek did not release the accepted in-flight prefix first.");
      Assert(events[2].Key == 64 && events[2].Down, "Latest seek did not restore its held state.");
      Assert(pump.Snapshot.PartialRetries == 0, "Invalidated input was retried.");
      pump.ReleaseAll();
      Assert(SpinWait.SpinUntil(() => pump.Snapshot.Emitted == 4, 2000), "Final held key was not released.");
      Assert(!emitter.Events()[3].Down, "Release-all emitted a key-down.");
    }
    finally
    {
      emitter.Continue.Set();
      pump.Dispose();
      Assert(waiter.Disposed.Wait(2000), "Seek test cleanup did not complete.");
    }
  }

  private static void TestPauseCancelsOverdueInput()
  {
    using var emitter = new BlockingEmitter();
    var waiter = new TrackingWaiter();
    var clock = new ManualClock();
    var pump = new ReplayNativeInputPump(new ReplayInputScheduler(Chord()), emitter, clock, waiter);
    try
    {
      pump.ResetTo(-1, 1, true);
      clock.Time = 100_000;
      pump.Synchronize(100_000, 1, true);
      Assert(emitter.Entered.Wait(2000), "Pause test never reached overdue native emission.");
      AssertResponsive(() => pump.SuspendAt(15_000));
      emitter.Continue.Set();
      Assert(SpinWait.SpinUntil(() => pump.Snapshot.Emitted == 2, 2000), "Pause did not release the accepted key.");
      Assert(pump.Snapshot.NextIndex == 4, "Pause did not seek past the released chord.");
      AssertOnlyPressAndRelease(emitter.Events(), "Pause emitted the overdue backlog or a partial retry.");
    }
    finally
    {
      emitter.Continue.Set();
      pump.Dispose();
      Assert(waiter.Disposed.Wait(2000), "Pause test cleanup did not complete.");
    }
  }

  private static void TestFocusResumePreservesReleaseBoundary()
  {
    using var emitter = new BlockingEmitter();
    var waiter = new TrackingWaiter();
    var clock = new ManualClock();
    var pump = new ReplayNativeInputPump(new ReplayInputScheduler(Chord()), emitter, clock, waiter);
    try
    {
      pump.ResetTo(0, 1, true);
      Assert(emitter.Entered.Wait(2000), "Focus test never reached native emission.");
      AssertResponsive(() =>
      {
        pump.Synchronize(15_000, 1, false);
        pump.Synchronize(15_000, 1, true);
      });
      emitter.Continue.Set();
      Assert(SpinWait.SpinUntil(() => pump.Snapshot.StateSeeks == 2, 2000), "Focus resume was not applied.");
      AssertOnlyPressAndRelease(emitter.Events(), "Coalescing focus updates lost the release boundary.");
    }
    finally
    {
      emitter.Continue.Set();
      pump.Dispose();
      Assert(waiter.Disposed.Wait(2000), "Focus test cleanup did not complete.");
    }
  }

  private static void TestHeartbeatUsesPublicationTime()
  {
    using var emitter = new BlockingEmitter();
    var waiter = new TrackingWaiter();
    var clock = new ManualClock();
    var inputs = new List<RecordedInput> { Input(0, 61, true), Input(900_000, 61, false) };
    var pump = new ReplayNativeInputPump(new ReplayInputScheduler(inputs), emitter, clock, waiter);
    try
    {
      pump.ResetTo(0, 1, true);
      Assert(emitter.Entered.Wait(2000), "Clock test never reached native emission.");
      clock.Time = 500_000;
      AssertResponsive(() => pump.Synchronize(500_000, 1, true));
      clock.Time = 1_000_000;
      emitter.Continue.Set();
      Assert(SpinWait.SpinUntil(() => pump.Finished, 2000), "Worker delay was incorrectly added to the replay clock.");
      AssertOnlyPressAndRelease(emitter.Events(), "Delayed heartbeat lost the due key-up.");
      pump.ResetTo(-1_000_000, 1, true);
      Assert(!pump.Finished, "A pending restart was reported as already finished.");
    }
    finally
    {
      emitter.Continue.Set();
      pump.Dispose();
      Assert(waiter.Disposed.Wait(2000), "Clock test cleanup did not complete.");
    }
  }

  private static void AssertOnlyPressAndRelease(NativeInputEmission[] events, string message)
  {
    Assert(
      events.Length == 2 && events[0].Key == 61 && events[0].Down && events[1].Key == 61 && !events[1].Down,
      message
    );
  }

  private static void TestReplacementWaitsForPreviousRelease()
  {
    using var emitter = new BlockingEmitter();
    var firstWaiter = new TrackingWaiter();
    var secondWaiter = new TrackingWaiter();
    var first = new ReplayNativeInputPump(new ReplayInputScheduler(Chord()), emitter, new ManualClock(), firstWaiter);
    ReplayNativeInputPump second = null;
    try
    {
      first.ResetTo(0, 1, true);
      Assert(emitter.Entered.Wait(2000), "Previous replay never reached native emission.");
      AssertResponsive(() =>
      {
        // Production constructs a replacement before disposing the old context.
        second = new ReplayNativeInputPump(
          new ReplayInputScheduler(new List<RecordedInput> { Input(0, 70, true), Input(10_000_000, 70, false) }),
          emitter,
          new ManualClock(),
          secondWaiter
        );
        first.Dispose();
        second.ResetTo(0, 1, true);
      });
      Assert(
        !SpinWait.SpinUntil(() => emitter.Events().Length > 1, 50),
        "Replacement emitted before the previous in-flight input was released."
      );
      emitter.Continue.Set();
      Assert(
        SpinWait.SpinUntil(() => second.Snapshot.Emitted == 1, 2000),
        "Replacement never acquired input ownership."
      );
      NativeInputEmission[] events = emitter.Events();
      Assert(events.Length == 3, "Replay replacement emitted an unexpected transition.");
      Assert(events[1].Key == 61 && !events[1].Down, "Old replay did not release its key before the replacement.");
      Assert(events[2].Key == 70 && events[2].Down, "New replay's press was overtaken by an old release.");
    }
    finally
    {
      emitter.Continue.Set();
      first.Dispose();
      second?.Dispose();
      Assert(firstWaiter.Disposed.Wait(2000), "Old replay cleanup did not complete.");
      if (second != null)
        Assert(secondWaiter.Disposed.Wait(2000), "Replacement cleanup did not complete.");
    }
  }

  private static void AssertResponsive(Action action)
  {
    Exception failure = null;
    var caller = new Thread(() =>
    {
      try
      {
        action();
      }
      catch (Exception exception)
      {
        failure = exception;
      }
    })
    {
      IsBackground = true,
    };
    caller.Start();
    Assert(caller.Join(1000), "Unity-side operation waited for a blocked native emitter.");
    Assert(failure == null, "Unity-side operation failed: " + failure);
  }

  private sealed class ManualClock : IReplayMonotonicClock
  {
    private long _time;
    public long Time
    {
      set => Interlocked.Exchange(ref _time, value);
    }
    public long Timestamp => Interlocked.Read(ref _time);
    public long Frequency => 1_000_000;
  }

  private sealed class TrackingWaiter : IReplayDeadlineWaiter
  {
    public readonly ManualResetEventSlim Disposed = new ManualResetEventSlim();
    public string Name => "test";
    public string FallbackReason => null;

    public ReplayDeadlineWaitResult WaitUntil(long deadlineTicks, AutoResetEvent wake, IReplayMonotonicClock clock)
    {
      wake.WaitOne(1);
      return ReplayDeadlineWaitResult.Woken;
    }

    public void Dispose() => Disposed.Set();
  }

  private sealed class BlockingEmitter : INativeInputEmitter, IDisposable
  {
    public readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
    public readonly ManualResetEventSlim Continue = new ManualResetEventSlim();
    private readonly List<NativeInputEmission> _events = new List<NativeInputEmission>();
    private int _calls;

    public bool IsSupported(int key) => true;

    public NativeInputEmitResult EmitBatch(NativeInputEmission[] emissions, int offset, int count)
    {
      bool first = Interlocked.Increment(ref _calls) == 1;
      int accepted = first ? 1 : count;
      lock (_events)
        for (int i = 0; i < accepted; i++)
          _events.Add(emissions[offset + i]);
      if (first)
      {
        Entered.Set();
        Continue.Wait();
      }
      return new NativeInputEmitResult(accepted);
    }

    public NativeInputEmission[] Events()
    {
      lock (_events)
        return _events.ToArray();
    }

    public void Dispose()
    {
      Entered.Dispose();
      Continue.Dispose();
    }
  }
}
