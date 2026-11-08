using System.Diagnostics;
using System.Reflection;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Models;
using static TestFixture;

internal static class CGEventCaptureRegressionSuite
{
  private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

  internal static void RunAll()
  {
    TestFocusBoundaryAndHeldState();
    TestHeldKeyAcrossExtendedSlots();
    TestCaptureFaultsPreventPlayback();
    TestCaptureOverflowPreventsPlayback();
  }

  private static void TestFocusBoundaryAndHeldState()
  {
    WithSource(source =>
    {
      RecordInputTracker.StartCapture();
      long boundary = RecordInputTracker.CaptureStartTimestampTicks;
      Assert(boundary > 0 && boundary <= Stopwatch.GetTimestamp(), "Input capture did not expose its real boundary.");
      source.Held = true;
      RecordInputTracker.SetCaptureWindowActive(true);
      var initial = Drain();
      Assert(
        initial.Length == 1 && initial[0].Key == 128 && initial[0].Down,
        "Focus entry lost the held mouse-button checkpoint."
      );
      long oldTimestamp = Stopwatch.GetTimestamp();
      RecordInputTracker.SetCaptureWindowActive(false);
      source.Held = false;
      source.Emit(oldTimestamp, false); // Released in another app.
      RecordInputTracker.SetCaptureWindowActive(true);
      var resumed = Drain();
      Assert(resumed.Length == 1 && !resumed[0].Down, "Focus resume did not release the previously held button.");
      source.Emit(oldTimestamp, true); // Delayed native backlog from before resume.
      Assert(Drain().Length == 0, "Focus resume accepted stale input backlog.");
      source.Emit(Stopwatch.GetTimestamp(), true);
      Assert(Drain().Length == 1, "Current foreground input was rejected.");
      Assert(
        RecordInputTracker.CaptureStartTimestampTicks == boundary,
        "Focus changes moved the run's media boundary."
      );
      RecordInputTracker.StopCapture(null);
      Assert(RecordInputTracker.CaptureStartTimestampTicks == 0, "Stopped input exposed a stale media boundary.");
    });
  }

  // Korean layouts send Right Alt as an extended VK_HANGUL, but a state snapshot reads only the
  // virtual key and holds it under the non-extended slot. Both slots must stay one key.
  private static void TestHeldKeyAcrossExtendedSlots()
  {
    WithSource(
      new FakeSource(0x15, usesExtendedKeyState: true),
      source =>
      {
        RecordInputTracker.StartCapture();
        source.Held = true; // The key that started the level is still down.
        RecordInputTracker.SetCaptureWindowActive(true);
        var pressed = Drain();
        Assert(
          pressed.Length == 1 && pressed[0].Down && !pressed[0].ExtendedKey,
          "Capture start lost the held Hangul key."
        );
        source.Held = false;
        source.Emit(Stopwatch.GetTimestamp(), false, true, 0x38, 0x81);
        var released = Drain();
        Assert(
          released.Length == 1 && !released[0].Down && !released[0].ExtendedKey && released[0].NativeCode == 0x38,
          "A Hangul key held when capture began was never released."
        );

        source.Emit(Stopwatch.GetTimestamp(), true, true, 0x38, 0x01);
        Assert(Drain().Length == 1, "The hook's extended Hangul press was rejected.");
        RecordInputTracker.SetCaptureWindowActive(false);
        source.Held = true;
        RecordInputTracker.SetCaptureWindowActive(true);
        Assert(Drain().Length == 0, "Focus resume pressed a key the hook already held under its extended slot.");
        RecordInputTracker.SetCaptureWindowActive(false);
        source.Held = false; // Released in another app.
        RecordInputTracker.SetCaptureWindowActive(true);
        var away = Drain();
        Assert(
          away.Length == 1 && !away[0].Down && away[0].ExtendedKey,
          "Focus resume did not release the extended slot the hook held."
        );
        RecordInputTracker.StopCapture(null);
      }
    );
  }

  private static void TestCaptureFaultsPreventPlayback()
  {
    foreach (
      string reason in new[]
      {
        ReplayUnavailableReasons.InputTapTimeout,
        ReplayUnavailableReasons.InputTapDisabled,
        ReplayUnavailableReasons.InputEventDelayed,
        ReplayUnavailableReasons.InputSourceStopped,
      }
    )
      WithSource(source =>
      {
        RecordInputTracker.StartCapture();
        source.Failure = reason;
        RecordInputTracker.StopCapture(null); // Also checks faults arriving at the final boundary.
        var payload = new RecordedRunPayload();
        RecordInputTracker.CopyDiagnosticsTo(payload);
        var run = RecordingPayloadBuilder.Apply(new RunRecord { Id = "fault" }, payload);
        Assert(
          run.ReplayArtifact == null && run.ReplayUnavailableReason == reason,
          "Capture fault was lost or allowed a playable artifact: " + reason
        );
        Assert(payload.ToActivityMetaJson().Contains(reason), "Capture metadata lost the fault.");
        RecordInputTracker.StartCapture();
        RecordInputTracker.StopCapture(null);
        var next = new RecordedRunPayload();
        RecordInputTracker.CopyDiagnosticsTo(next);
        Assert(next.GetInputFailureReason() == null, "A previous run's fault contaminated a retry.");
      });
  }

  private static void TestCaptureOverflowPreventsPlayback()
  {
    WithSource(source =>
    {
      source.Dropped = 10; // Idle faults must not contaminate a new run.
      RecordInputTracker.StartCapture();
      source.Dropped = 3;
      RecordInputTracker.StopCapture(null);
      var payload = new RecordedRunPayload();
      RecordInputTracker.CopyDiagnosticsTo(payload);
      var run = RecordingPayloadBuilder.Apply(new RunRecord { Id = "overflow" }, payload);
      Assert(payload.InputOverflowDropped == 3, "Capture drop counter included idle drops.");
      Assert(
        run.ReplayArtifact == null && run.ReplayUnavailableReason == ReplayUnavailableReasons.InputQueueOverflow,
        "Overflowed recording remained playable."
      );
    });
  }

  private static NativeInputTransition[] Drain()
  {
    int count = (int)typeof(RecordInputTracker).GetMethod("DrainFilteredTransitions", PrivateStatic).Invoke(null, null);
    var buffer = (NativeInputTransition[])
      typeof(RecordInputTracker).GetField("FilteredDrainBuffer", PrivateStatic).GetValue(null);
    return buffer.Take(count).ToArray();
  }

  private static void WithSource(Action<FakeSource> test) => WithSource(new FakeSource(), test);

  private static void WithSource(FakeSource source, Action<FakeSource> test)
  {
    FieldInfo field = typeof(RecordInputTracker).GetField("EventSource", PrivateStatic);
    var previous = (INativeInputEventSource)field.GetValue(null);
    field.SetValue(null, source);
    try
    {
      test(source);
    }
    finally
    {
      RecordInputTracker.Reset();
      field.SetValue(null, previous);
      source.Stop();
    }
  }

  private sealed class FakeSource : INativeInputEventSource, INativeInputCaptureHealth
  {
    private readonly int _key;
    private Action<NativeInputTransition> _callback;
    public bool Held;
    public long Dropped;
    public string Failure;

    public FakeSource(int key = 128, bool usesExtendedKeyState = false)
    {
      _key = key;
      UsesExtendedKeyState = usesExtendedKeyState;
    }

    public string Name => "test-cgevent";
    public bool IsRunning { get; private set; }
    public bool UsesExtendedKeyState { get; }
    public IReadOnlyList<int> SnapshotKeyCodes => new[] { _key };

    public void Start(Action<NativeInputTransition> callback)
    {
      _callback = callback;
      IsRunning = true;
    }

    public void Stop()
    {
      IsRunning = false;
    }

    public void RefreshPhysicalState() { }

    public bool TryGetPhysicalKeyState(int key, out bool down)
    {
      down = Held;
      return true;
    }

    public long ConsumeDroppedEvents()
    {
      long dropped = Dropped;
      Dropped = 0;
      return dropped;
    }

    public string ConsumeCaptureFailure()
    {
      string failure = Failure;
      Failure = null;
      return failure;
    }

    public NativeInputSourceDiagnostics GetDiagnostics() => default;

    public void Emit(long timestamp, bool down) => _callback(new NativeInputTransition(timestamp, 0, _key, down));

    public void Emit(long timestamp, bool down, bool extended, int nativeCode, ulong nativeFlags) =>
      _callback(new NativeInputTransition(timestamp, 0, _key, down, extended, nativeCode, nativeFlags));
  }
}
