using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Queries;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Calibration.Analysis;
using TUFReplay.Calibration.Levels;
using TUFReplay.Calibration.Models;
using TUFReplay.Calibration.Playback;
using TUFReplay.Calibration.Sessions;
using TUFReplay.Microphone.Capture;
using TUFReplay.Microphone.Devices;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Playback;
using TUFReplay.Microphone.Processing;
using TUFReplay.Microphone.Recording;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Microphone.Timing;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Models;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Patches;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Timeline;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.Database;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Unity;
using static TestFixture;

internal static class ReplayNativeInputSuite
{
  internal static void RunAll()
  {
    TestReplayInputStableOrder();
    TestReplayCsvParserCompatibility();
    TestNativeInputCsvRoundTrip();
    TestInputTimelineMath();
    TestReplayLatenessHistogram();
    TestNativeInputRingBufferStress();
    TestReplayTimelineTimeMath();
    TestReplayTimelineTopGlowPolicy();
    TestReplayTimelineJudgmentMapping();
    TestReplayTimelineLegacyJudgmentTimeMath();
    TestLegacyJudgmentOverloadMath();
    TestHitContextPlaybackPositionComparison();
    TestReplayNoFailPolicy();
    TestNativeInputUmmWindowInterlock();
    TestWindowsNativeModifierNormalization();
    TestWindowsPhysicalStateUsesCurrentDownBit();
    TestUnsupportedCaptureHasNoPollingFallback();
    TestReplayInputFormatGate();
    TestCrossPlatformNativeInputFallback();
    TestWindowsPhysicalKeyMetadata();
    TestReplaySchedulerChord();
    TestReplayPumpTimingAndBatching();
    TestReplayPumpFocusAndReleaseAll();
    TestReplayPumpPauseSuspendsAndResumes();
    TestReplayPumpClockJumpPreservesBacklog();
    TestReplayPumpCatchUpYieldBoundary();
    TestReplayPumpPartialEmissionRetry();
    TestPreparedReplayDoesNotEmit();
    TestMiddleStartReplayInitializesFromPlayerControl();
  }

  private static void TestReplayInputStableOrder()
  {
    byte[] csv = System.Text.Encoding.UTF8.GetBytes("100,9,3\n100,8,3\n100,9,2\n99,7,3\n");
    List<RecordedInput> inputs = ReplayInputParser.Parse(csv, out long maxTimeUs);

    Assert(inputs.Count == 4, "Replay input parser dropped valid events.");
    Assert(maxTimeUs == 100, "Replay input parser did not report the maximum timestamp.");
    Assert(inputs[0].Key == 7, "Replay input parser did not sort timestamps.");
    Assert(inputs[1].Key == 9 && inputs[2].Key == 8 && inputs[3].Key == 9, "Same-time input order changed.");
  }

  private static void TestReplayCsvParserCompatibility()
  {
    byte[] inputCsv = System.Text.Encoding.UTF8.GetBytes("\r\n 200 , 11 , 3 \r\ninvalid\n100,7,2\r\n200,12,1\n");
    List<RecordedInput> inputs = ReplayInputParser.Parse(inputCsv);

    Assert(inputs.Count == 3, "Replay input parser did not ignore malformed or empty lines.");
    Assert(inputs[0].TimeUs == 100 && inputs[0].Key == 7, "Replay input parser changed legacy sorting.");
    Assert(inputs[1].Key == 11 && inputs[2].Key == 12, "Replay input parser changed stable tie ordering.");

    byte[] hitCsv = System.Text.Encoding.UTF8.GetBytes(
      "\n1,90.5,1.25,1,False,TRUE,-2.5,3E2,0,true,-4\r\nmalformed\r\n"
    );
    List<ReplayHitContext> contexts = ReplayHitContextParser.Parse(hitCsv);

    Assert(contexts.Count == 1, "Replay hit context parser did not ignore malformed or empty lines.");
    ReplayHitContext context = contexts[0];
    Assert(context.CurrentFloorID == 1 && context.CurrAngle == 90.5, "Replay hit context numbers changed.");
    Assert(context.OverloadCounter == 1.25f && context.TargetExitAngle == 300, "Replay float parsing changed.");
    Assert(context.NoFailHit && !context.IsAuto && context.NextFloorAuto, "Replay boolean parsing changed.");
    Assert(!context.MidspinInfiniteMargin && context.RDCAuto, "Replay boolean flag parsing changed.");
    Assert(context.CurFreeRoamSection == -4, "Replay signed integer parsing changed.");
    Assert(!context.ResolvedHitMargin.HasValue, "Legacy replay unexpectedly gained a resolved hit margin.");

    byte[] resolvedHitCsv = System.Text.Encoding.UTF8.GetBytes("2,0,0,0,0,0,1,2,0,0,0,4\n");
    ReplayHitContext resolvedContext = ReplayHitContextParser.Parse(resolvedHitCsv)[0];
    Assert(resolvedContext.ResolvedHitMargin == 4, "Resolved hit margin was not preserved by the replay parser.");
    Assert(!resolvedContext.TimeUs.HasValue, "A 12-column replay unexpectedly gained a judgment timestamp.");

    var payload = new RecordedRunPayload();
    payload.HitContexts.Add(
      new RecordedHitContext
      {
        CurrentFloorID = 3,
        CurrAngle = -0.25d,
        OverloadCounter = 0.75f,
        CachedAngle = 1.5d,
        TargetExitAngle = 2.5d,
        CurFreeRoamSection = 2,
        ResolvedHitMargin = 4,
        TimeUs = 1_234_567L,
      }
    );
    ReplayHitContext timedContext = ReplayHitContextParser.Parse(payload.ToHitContextCsvBytes())[0];
    Assert(timedContext.ResolvedHitMargin == 4, "13-column hit margin round-trip failed.");
    Assert(timedContext.TimeUs == 1_234_567L, "13-column judgment timestamp round-trip failed.");

    byte[] malformedTimedCsv = System.Text.Encoding.UTF8.GetBytes("2,0,0,0,0,0,1,2,0,0,0,4,not-a-time\n");
    Assert(ReplayHitContextParser.Parse(malformedTimedCsv).Count == 0, "Malformed 13-column timestamp was accepted.");
  }

  private static void TestNativeInputCsvRoundTrip()
  {
    var payload = new RecordedRunPayload();
    payload.Inputs.Add(new RecordedInput(-125_000, 0xA2, RecordInputFlags.Async | RecordInputFlags.Down, 0x1D, 0x11));
    payload.Inputs.Add(new RecordedInput(250, 0xA2, RecordInputFlags.Async, 0x1D, 0x91));

    List<RecordedInput> parsed = ReplayInputParser.Parse(payload.ToInputCsvBytes());
    Assert(parsed.Count == 2, "Five-column input CSV did not round-trip.");
    Assert(
      parsed[0].TimeUs == -125_000
        && parsed[0].NativeCode == 0x1D
        && parsed[0].NativeFlags == 0x11
        && parsed[0].Down,
      "Five-column native metadata changed during round-trip."
    );
    Assert(parsed[1].NativeFlags == 0x91 && !parsed[1].Down, "Key-up native provenance was not preserved.");

    byte[] malformed = System.Text.Encoding.UTF8.GetBytes("1,65,3,30,not-a-flag\n2,66,3,31,1\n");
    parsed = ReplayInputParser.Parse(malformed);
    Assert(parsed.Count == 1 && parsed[0].Key == 66, "Malformed native metadata was accepted or hid valid rows.");

  }

  private static void TestInputTimelineMath()
  {
    long frequency = System.Diagnostics.Stopwatch.Frequency;
    int[] frameRates = { 30, 60, 144, 240 };
    foreach (int frameRate in frameRates)
    {
      long frameTicks = frequency / frameRate;
      long eventTicks = frameTicks * 3 / 4;
      long mapped = InputTimelineMath.Interpolate(eventTicks, 0, 0, frameTicks, 1_000_000L / frameRate);
      long expected = 750_000L / frameRate;
      Assert(Math.Abs(mapped - expected) <= 2, "Frame-interior input interpolation changed at " + frameRate + " FPS.");
    }

    long stallTicks = frequency / 4;
    long stallMapped = InputTimelineMath.Interpolate(stallTicks / 2, 0, 1_000_000, stallTicks, 1_250_000);
    Assert(stallMapped == 1_125_000, "250 ms frame-stall interpolation attached input to a frame boundary.");

    long countdown = InputTimelineMath.BackProject(0, frequency / 2, 0, 1.0);
    Assert(Math.Abs(countdown + 500_000) <= 1, "First-anchor countdown back-projection lost negative time.");
    long pitched = InputTimelineMath.BackProject(0, frequency / 2, 0, 1.5);
    Assert(Math.Abs(pitched + 750_000) <= 1, "Pitch was not applied to first-anchor back-projection.");
  }

  private static void TestReplayLatenessHistogram()
  {
    var histogram = new ReplayLatenessHistogram();
    for (int i = 1; i <= 100; i++)
      histogram.Record(i * 50L - 1L);
    Assert(histogram.Count == 100, "Replay lateness histogram lost samples.");
    Assert(histogram.Percentile(0.50d) == 2_500L, "Replay lateness p50 bucket changed.");
    Assert(histogram.Percentile(0.95d) == 4_750L, "Replay lateness p95 bucket changed.");
    Assert(histogram.Percentile(0.99d) == 4_950L, "Replay lateness p99 bucket changed.");
    histogram.Record(75_000L);
    Assert(histogram.MaxUs == 75_000L, "Replay lateness overflow bucket lost max latency.");
  }

  private static void TestNativeInputRingBufferStress()
  {
    var buffer = new NativeInputTransitionRingBuffer();
    var drain = new NativeInputTransition[256];
    int expected = 0;
    int drainedTotal = 0;
    for (int chord = 0; chord < 1_000; chord++)
    {
      for (int key = 0; key < 32; key++)
      {
        int sequence = chord * 32 + key;
        Assert(
          buffer.TryEnqueue(new NativeInputTransition(sequence, sequence, key + 1, true, false, key + 1, 0)),
          "Ring buffer overflowed during regularly drained 32-key chord stress."
        );
      }

      if ((chord & 3) != 3)
        continue;
      int count = buffer.DrainTo(drain);
      for (int i = 0; i < count; i++)
        Assert(drain[i].CaptureTimestampTicks == expected++, "Ring buffer changed native callback ordering.");
      drainedTotal += count;
    }

    int remaining;
    while ((remaining = buffer.DrainTo(drain)) > 0)
    {
      for (int i = 0; i < remaining; i++)
        Assert(drain[i].CaptureTimestampTicks == expected++, "Ring buffer changed tail ordering.");
      drainedTotal += remaining;
    }
    Assert(drainedTotal == 32_000, "Ring buffer stress test lost transitions.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x10, 0x36, false) == 0xA1, "Right Shift was not normalized.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x11, 0x1D, true) == 0xA3, "Right Ctrl was not normalized.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x12, 0x38, true) == 0xA5, "Right Alt was not normalized.");
  }

  private static void TestLegacyJudgmentOverloadMath()
  {
    Assert(
      !ReplayLegacyJudgmentMath.BecomesFailOverload(0.5f, drumController: false, purePerfectOnly: false, noFail: false),
      "An overload counter of exactly one was treated as FailOverload."
    );
    Assert(
      ReplayLegacyJudgmentMath.BecomesFailOverload(
        0.5001f,
        drumController: false,
        purePerfectOnly: false,
        noFail: false
      ),
      "A Too Early hit crossing the overload threshold was not promoted to FailOverload."
    );
    Assert(
      !ReplayLegacyJudgmentMath.BecomesFailOverload(0.7f, drumController: true, purePerfectOnly: false, noFail: false),
      "Drum-controller overload damage did not use ADOFAI's reduced amount."
    );
    Assert(
      !ReplayLegacyJudgmentMath.BecomesFailOverload(0.9f, drumController: false, purePerfectOnly: true, noFail: false),
      "Pure Perfect mode incorrectly replaced Too Early with FailOverload."
    );
    Assert(
      ReplayLegacyJudgmentMath.BecomesFailOverload(0.9f, drumController: false, purePerfectOnly: true, noFail: true),
      "No-fail Pure Perfect mode did not preserve ADOFAI's FailOverload promotion."
    );
  }

  private static void TestReplayTimelineTimeMath()
  {
    var cleared = new ActiveReplayContext
    {
      Result = "cleared",
      TerminalTimeUs = 12_000_000L,
      Meta = new ReplayMetadata { wonTimeUs = 8_000_000L },
    };
    var failed = new ActiveReplayContext
    {
      Result = "failed",
      TerminalTimeUs = 12_000_000L,
      Meta = new ReplayMetadata { wonTimeUs = 8_000_000L },
    };

    Assert(
      ReplaySessionService.TimelineDurationTimeUs(cleared) == 8_000_000L,
      "Cleared timeline did not stop at wonTimeUs."
    );
    Assert(
      ReplaySessionService.TimelineDurationTimeUs(failed) == 12_000_000L,
      "Failed timeline did not use terminalTimeUs."
    );
    Assert(
      ReplaySessionService.ClampTimelineSeekTime(-1L, 8_000_000L) == 0L,
      "Timeline seek did not clamp to the beginning."
    );
    Assert(
      ReplaySessionService.ClampTimelineSeekTime(8_000_000L, 8_000_000L) == 7_999_999L,
      "Timeline seek did not stay before the terminal boundary."
    );
    Assert(
      Math.Abs(ReplaySessionService.ToNormalizedTimelineTime(4_000_000L, 8_000_000L) - 0.5f) < 0.0001f,
      "Timeline normalized time calculation changed."
    );
  }

  private static void TestReplayTimelineTopGlowPolicy()
  {
    Assert(
      ReplaySessionService.ShouldTimelineTopGlowBeActive(hasLit: true, tileFlashStyle: 0),
      "A passed floor lost its normal timeline top glow."
    );
    Assert(
      !ReplaySessionService.ShouldTimelineTopGlowBeActive(hasLit: false, tileFlashStyle: 0),
      "A future floor retained its normal timeline top glow."
    );
    Assert(
      ReplaySessionService.ShouldTimelineTopGlowBeActive(hasLit: false, tileFlashStyle: 4),
      "AlwaysOn did not preserve the future floor top glow."
    );
    Assert(
      !ReplaySessionService.ShouldTimelineTopGlowBeActive(hasLit: true, tileFlashStyle: 5),
      "AlwaysBlack enabled a passed floor top glow."
    );
  }

  private static void TestReplayTimelineJudgmentMapping()
  {
    var expected = new (int Margin, ReplayTimelineJudgmentKind Kind)[]
    {
      (9, ReplayTimelineJudgmentKind.Overload),
      (0, ReplayTimelineJudgmentKind.TooEarly),
      (1, ReplayTimelineJudgmentKind.Early),
      (2, ReplayTimelineJudgmentKind.EarlyPerfect),
      (3, ReplayTimelineJudgmentKind.Perfect),
      (10, ReplayTimelineJudgmentKind.Perfect),
      (4, ReplayTimelineJudgmentKind.LatePerfect),
      (5, ReplayTimelineJudgmentKind.Late),
      (6, ReplayTimelineJudgmentKind.TooLate),
      (8, ReplayTimelineJudgmentKind.Miss),
    };

    foreach ((int margin, ReplayTimelineJudgmentKind expectedKind) in expected)
    {
      Assert(
        ReplayTimelineJudgmentMath.TryMapHitMarginValue(margin, out ReplayTimelineJudgmentKind actualKind)
          && actualKind == expectedKind,
        "Replay timeline judgment mapping changed for " + margin + "."
      );
    }

    Assert(
      !ReplayTimelineJudgmentMath.TryMapHitMarginValue(7, out _),
      "Multipress was included in timeline judgments."
    );
    Assert(
      !ReplayTimelineJudgmentMath.TryMapHitMarginValue(11, out _),
      "OverPress was included in timeline judgments."
    );
  }

  private static void TestReplayTimelineLegacyJudgmentTimeMath()
  {
    Assert(
      ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(
        10d,
        0d,
        Math.PI,
        60d,
        1d,
        1d,
        20_000_000L,
        out long lateTimeUs
      )
        && lateTimeUs == 11_000_000L,
      "Positive legacy judgment angle did not produce the expected late offset."
    );
    Assert(
      ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(
        10d,
        0d,
        -Math.PI,
        60d,
        1d,
        1d,
        20_000_000L,
        out long earlyTimeUs
      )
        && earlyTimeUs == 9_000_000L,
      "Negative legacy judgment angle did not produce the expected early offset."
    );
    Assert(
      ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(
        10d,
        0d,
        Math.PI,
        120d,
        2d,
        2d,
        20_000_000L,
        out long scaledTimeUs
      )
        && scaledTimeUs == 10_125_000L,
      "Legacy judgment timing did not apply BPM, floor speed, and pitch."
    );
    Assert(
      ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(30d, 0d, 0d, 60d, 1d, 1d, 20_000_000L, out long clampedTimeUs)
        && clampedTimeUs == 20_000_000L,
      "Legacy judgment timestamp did not clamp to replay duration."
    );
    Assert(
      !ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(double.NaN, 0d, 0d, 60d, 1d, 1d, 20_000_000L, out _),
      "Invalid legacy judgment metadata was accepted."
    );
    Assert(
      !ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(10d, 0d, 0d, 60d, 1d, 0d, 20_000_000L, out _),
      "Invalid legacy judgment pitch was accepted."
    );
  }

  private static void TestHitContextPlaybackPositionComparison()
  {
    Assert(
      ReplayHitContextPlayer.ComparePlaybackPosition(2627, 0, 2628, 0) < 0,
      "A future hit-context floor was not treated as pending."
    );
    Assert(
      ReplayHitContextPlayer.ComparePlaybackPosition(2628, 0, 2628, 0) == 0,
      "The current hit-context position did not compare equal."
    );
    Assert(
      ReplayHitContextPlayer.ComparePlaybackPosition(2629, 0, 2628, 0) > 0,
      "A skipped hit-context floor was not treated as an error."
    );
    Assert(
      ReplayHitContextPlayer.ComparePlaybackPosition(2628, 1, 2628, 0) > 0,
      "A skipped free-roam section was not treated as an error."
    );
  }

  private static void TestReplayNoFailPolicy()
  {
    Assert(!ReplayFailPolicy.ShouldUseReplayNoFail(null), "Missing replay context enabled No-Fail.");
    Assert(!ReplayFailPolicy.ShouldUseReplayNoFail(new ActiveReplayContext()), "A normal replay enabled No-Fail.");
    Assert(
      ReplayFailPolicy.ShouldUseReplayNoFail(new ActiveReplayContext { NoFailMode = true }),
      "A No-Fail replay did not enable No-Fail."
    );
  }

  private static void TestNativeInputUmmWindowInterlock()
  {
    NativeInputUmmWindowInterlock.Reset();
    NativeInputUmmWindowInterlock.SetWindowOpenAt(true, 1_000L);
    Assert(NativeInputUmmWindowInterlock.IsBlockedAt(1_000L), "UMM open did not block native input.");

    NativeInputUmmWindowInterlock.SetWindowOpenAt(false, 2_000L);
    Assert(
      NativeInputUmmWindowInterlock.IsBlockedAt(2_000L),
      "UMM close did not preserve the native-input stabilization window."
    );
    Assert(
      !NativeInputUmmWindowInterlock.IsBlockedAt(long.MaxValue),
      "Native input remained blocked after the stabilization window."
    );

    NativeInputUmmWindowInterlock.Reset();
    Assert(NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(10_000L), "UMM fallback did not poll immediately.");
    Assert(
      !NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(10_000L),
      "UMM fallback polled twice in the same interval."
    );
    Assert(
      !NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(
        10_000L + NativeInputUmmWindowInterlock.FallbackPollIntervalTicks - 1
      ),
      "UMM fallback ignored its polling interval."
    );
    Assert(
      NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(
        10_000L + NativeInputUmmWindowInterlock.FallbackPollIntervalTicks
      ),
      "UMM fallback did not resume after its polling interval."
    );
    NativeInputUmmWindowInterlock.ConfigureManagerWindowPatch(available: true, synchronize: false);
    Assert(
      !NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(long.MaxValue),
      "UMM reflection fallback remained active after the Harmony patch was available."
    );
    NativeInputUmmWindowInterlock.Reset();
  }

  private static void TestReplaySchedulerChord()
  {
    var inputs = new List<RecordedInput>();
    for (int key = 1; key <= 128; key++)
      inputs.Add(Input(100, key, true));

    var scheduler = new ReplayInputScheduler(inputs);
    var chord = new List<RecordedInput>();
    Assert(scheduler.CopyNextTimestampGroup(chord) == 128, "128-key chord was truncated.");
    for (int i = 0; i < chord.Count; i++)
      Assert(chord[i].Key == i + 1, "Chord order changed.");
  }

  private static void TestWindowsPhysicalKeyMetadata()
  {
    RecordInputFlags mainEnterFlags = RecordInputFlags.Async | RecordInputFlags.Down;
    RecordInputFlags keypadEnterFlags = mainEnterFlags | RecordInputFlags.ExtendedKey;
    var inputs = new List<RecordedInput>
    {
      new RecordedInput(0, 0x0D, mainEnterFlags),
      new RecordedInput(0, 0x0D, keypadEnterFlags),
    };

    var scheduler = new ReplayInputScheduler(inputs);
    List<NativeInputKey> held = scheduler.SeekToNativeState(0);
    Assert(held.Count == 2, "Main Enter and numpad Enter collapsed into one held key.");
    Assert(held.Exists(key => key.Key == 0x0D && !key.ExtendedKey), "Main Enter identity was lost.");
    Assert(held.Exists(key => key.Key == 0x0D && key.ExtendedKey), "Numpad Enter identity was lost.");

    var payload = new RecordedRunPayload();
    payload.Inputs.AddRange(inputs);
    List<RecordedInput> roundTrip = ReplayInputParser.Parse(payload.ToInputCsvBytes());
    Assert(roundTrip.Count == 2 && roundTrip[1].ExtendedKey, "Extended-key flag was not preserved in CSV.");

    Assert(WindowsNativeInputKey.IsExtended(0x2C), "Print Screen must be treated as extended.");
    Assert(WindowsNativeInputKey.IsExtended(0x5D), "Menu/Application must be treated as extended.");
    Assert(!WindowsNativeInputKey.IsExtended(0x90), "Num Lock must not use the E0 extended-key flag.");
    Assert(!WindowsNativeInputKey.IsExtended(0xA0), "Left Shift must not be treated as extended.");

    var emitter = new WindowsNativeInputEmitter();
    Assert(emitter.IsSupported(0x1B), "Escape must be replayable.");
    Assert(!emitter.IsSupported(0x10), "Generic Shift must remain filtered to avoid duplicate modifier events.");
  }

  private static void TestCrossPlatformNativeInputFallback()
  {
    bool currentIsMac = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
      System.Runtime.InteropServices.OSPlatform.OSX
    );
    bool currentIsWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
      System.Runtime.InteropServices.OSPlatform.Windows
    );
    if (!currentIsMac && !currentIsWindows)
      return;

    string sourcePlatform = currentIsMac ? "windows" : "macos";
    int sourceA = currentIsMac ? 0x41 : 0x00;
    var meta = new ReplayMetadata
    {
      formatVersion = 3,
      inputKeySpace = NativeInputKeyCodeMapper.NativeKeySpace,
      inputNativePlatform = sourcePlatform,
      inputFormat = RecordedRunPayload.NativeInputFormatV2,
    };
    var foreignInputs = new List<RecordedInput>
    {
      new RecordedInput(1, sourceA, RecordInputFlags.Async | RecordInputFlags.Down, 0x1E, 0x91),
    };
    List<RecordedInput> normalized = NativeInputKeyCodeMapper.NormalizeForPlayback(foreignInputs, meta, out int dropped);
    Assert(dropped == 0 && normalized.Count == 1, "Cross-platform logical key fallback dropped a supported key.");
    Assert(
      NativeInputKeyCodeMapper.TryConvertLogicalKey(LogicalKeyboardKey.A, out int expectedA)
        && normalized[0].Key == expectedA,
      "Cross-platform logical key fallback produced the wrong current-platform key."
    );
    Assert(!normalized[0].HasNativeMetadata, "Foreign-platform scan code or flags leaked into native emission.");
  }

  private static void TestWindowsNativeModifierNormalization()
  {
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x10, 0x2A, false) == 0xA0, "Left Shift changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x10, 0x36, false) == 0xA1, "Right Shift changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x11, 0x1D, false) == 0xA2, "Left Ctrl changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x11, 0x1D, true) == 0xA3, "Right Ctrl changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x12, 0x38, false) == 0xA4, "Left Alt changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x12, 0x38, true) == 0xA5, "Right Alt changed.");
    Assert(WindowsLowLevelKeyboardEventSource.NormalizeModifierKey(0x45, 0x12, false) == 0x45, "Ordinary VK changed.");
  }

  private static void TestWindowsPhysicalStateUsesCurrentDownBit()
  {
    Assert(
      WindowsLowLevelKeyboardEventSource.IsAsyncKeyDown(unchecked((short)0x8000)),
      "Windows physical state ignored the current-down bit."
    );
    Assert(
      !WindowsLowLevelKeyboardEventSource.IsAsyncKeyDown(0x0001),
      "Windows physical state treated the recent-press bit as currently down."
    );
    Assert(!WindowsLowLevelKeyboardEventSource.IsAsyncKeyDown(0), "Windows physical state reported an idle key as down.");
  }

  private static void TestReplayInputFormatGate()
  {
    var current = new ReplayMetadata
    {
      formatVersion = 3,
      inputFormat = RecordedRunPayload.NativeInputFormatV2,
      inputTimeBase = ReplayInputTimeBases.Hybrid,
      inputKeySpace = NativeInputKeyCodeMapper.NativeKeySpace,
      inputNativePlatform = "windows",
      inputCapture = "windows-wh-keyboard-ll",
    };
    Assert(ReplayPlaybackCoordinator.HasCurrentNativeInputFormat(current), "Current five-column native input was rejected.");

    var legacy = new ReplayMetadata
    {
      formatVersion = 2,
      inputFormat = "csv-conductor-timeus-key-flags-v1",
      inputTimeBase = ReplayInputTimeBases.Hybrid,
      inputKeySpace = NativeInputKeyCodeMapper.NativeKeySpace,
      inputNativePlatform = "windows",
    };
    Assert(!ReplayPlaybackCoordinator.HasCurrentNativeInputFormat(legacy), "Legacy format was accepted without migration.");
    current.inputCapture = "skyhook-native-events";
    Assert(!ReplayPlaybackCoordinator.HasCurrentNativeInputFormat(current), "Legacy capture source was accepted without migration.");
    current.inputCapture = "windows-wh-keyboard-ll";
    current.inputNativePlatform = null;
    Assert(!ReplayPlaybackCoordinator.HasCurrentNativeInputFormat(current), "Missing native platform metadata was accepted.");
  }

  private static void TestUnsupportedCaptureHasNoPollingFallback()
  {
    if (
      System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
        System.Runtime.InteropServices.OSPlatform.Windows
      )
    )
      return;

    INativeInputEventSource source = NativeInputEventSourceFactory.CreatePrimary();
    Assert(source.Name == "unsupported-native-input", "A non-Windows capture fallback was enabled.");
    Assert(source.SnapshotKeyCodes.Count == 0, "Unsupported capture exposed polling keys.");
    bool rejected = false;
    try
    {
      source.Start(_ => { });
    }
    catch (PlatformNotSupportedException)
    {
      rejected = true;
    }
    Assert(rejected && !source.IsRunning, "Unsupported capture did not fail explicitly.");
  }

  private static void TestReplayPumpTimingAndBatching()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(
      new List<RecordedInput> { Input(0, 10, true), Input(0, 11, true), Input(200, 10, false) }
    );
    using var pump = new ReplayNativeInputPump(scheduler, emitter);

    pump.ResetTo(-20_000, 1d, true);
    Assert(emitter.WaitForBatchCount(2), "High-resolution pump did not emit both timestamps.");
    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches[0].Length == 2, "Same-time chord was not emitted as one batch.");
    Assert(batches[0][0].Key == 10 && batches[0][1].Key == 11, "Modifier/chord order changed.");
    Assert(batches[1].Length == 1 && !batches[1][0].Down, "200us key-up was merged or lost.");
  }

  private static void TestReplayPumpFocusAndReleaseAll()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(new List<RecordedInput> { Input(0, 20, true) });
    using var pump = new ReplayNativeInputPump(scheduler, emitter);

    pump.ResetTo(-20_000, 1d, true);
    Assert(emitter.WaitForBatchCount(1), "Initial held key was not emitted.");
    pump.Synchronize(100_000, 1d, false);
    pump.Synchronize(100_000, 1d, true);
    Assert(emitter.WaitForBatchCount(3), "Focus resume did not restore held state by delta.");
    pump.ReleaseAll();
    Assert(emitter.WaitForBatchCount(4), "Release-all did not release the restored key.");

    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches[1].Length == 1 && !batches[1][0].Down, "Focus loss did not release held keys.");
    Assert(batches[2].Length == 1 && batches[2][0].Down, "Focus resume replayed backlog instead of held state.");
    Assert(batches[3].Length == 1 && !batches[3][0].Down, "Release-all emitted an invalid transition.");
  }

  private static void TestReplayPumpPauseSuspendsAndResumes()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(
      new List<RecordedInput> { Input(0, 25, true), Input(1_000_000, 25, false) }
    );
    using var pump = new ReplayNativeInputPump(scheduler, emitter);

    pump.ResetTo(100_000, 1d, true);
    Assert(emitter.WaitForBatchCount(1), "Pause setup did not restore the held key.");
    pump.SuspendAt(100_000);
    Assert(emitter.WaitForBatchCount(2), "Pause did not release the held key.");
    Thread.Sleep(30);
    Assert(emitter.Snapshot().Count == 2, "Native input advanced while replay playback was paused.");

    pump.Synchronize(100_000, 1d, true);
    Assert(emitter.WaitForBatchCount(3), "Resume did not restore the held state at the paused timestamp.");
    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches[0].Length == 1 && batches[0][0].Down, "Pause setup did not emit key-down.");
    Assert(batches[1].Length == 1 && !batches[1][0].Down, "Pause did not emit key-up.");
    Assert(batches[2].Length == 1 && batches[2][0].Down, "Resume did not restore key-down.");
  }

  private static void TestReplayPumpClockJumpPreservesBacklog()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(
      new List<RecordedInput> { Input(0, 30, true), Input(10_000, 30, false), Input(20_000, 31, true) }
    );
    using var pump = new ReplayNativeInputPump(scheduler, emitter);

    pump.ResetTo(-1_000_000, 1d, true);
    pump.Synchronize(1_000_000, 1d, true);
    Assert(emitter.WaitForBatchCount(3), "Clock jump did not catch up every overdue transition.");
    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches.Count == 3, "Clock jump collapsed overdue transitions into held state.");
    Assert(batches[0][0].Key == 30 && batches[0][0].Down, "Clock catch-up lost the first key-down.");
    Assert(batches[1][0].Key == 30 && !batches[1][0].Down, "Clock catch-up lost the key-up.");
    Assert(batches[2][0].Key == 31 && batches[2][0].Down, "Clock catch-up ordering changed.");
    Assert(pump.Snapshot.StateSeeks == 1, "Clock residual triggered an implicit state seek.");
  }

  private static void TestReplayPumpPartialEmissionRetry()
  {
    var emitter = new PartialEmitter(firstEmissionCount: 1, retryEmissionCount: int.MaxValue);
    var scheduler = new ReplayInputScheduler(
      new List<RecordedInput> { Input(0, 41, true), Input(0, 42, true), Input(0, 43, true) }
    );
    using var pump = new ReplayNativeInputPump(scheduler, emitter);
    pump.ResetTo(-10_000, 1d, true);
    Assert(emitter.WaitForCallCount(2), "Partial native emission was not retried.");
    ReplayNativeInputStats stats = pump.Snapshot;
    Assert(stats.Emitted == 3 && stats.FailedEvents == 0, "Tail retry did not preserve every event.");
    Assert(stats.PartialRetries == 1, "Partial native emission retry was not counted.");
    Assert(emitter.Offsets[0] == 0 && emitter.Offsets[1] == 1, "Partial retry resent an emitted prefix.");
  }

  private static void TestReplayPumpCatchUpYieldBoundary()
  {
    var emitter = new CapturingEmitter();
    var inputs = new List<RecordedInput>();
    for (int i = 0; i < 300; i++)
      inputs.Add(Input(i * 1_000L, i + 1_000, true));
    var scheduler = new ReplayInputScheduler(inputs);
    using var pump = new ReplayNativeInputPump(scheduler, emitter);
    pump.ResetTo(-1_000_000, 1d, true);
    pump.Synchronize(1_000_000, 1d, true);
    Assert(emitter.WaitForBatchCount(300), "Catch-up stopped at the 256-group yield boundary.");
    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches.Count == 300, "Catch-up yield boundary lost timestamp groups.");
    for (int i = 0; i < batches.Count; i++)
      Assert(batches[i].Length == 1 && batches[i][0].Key == i + 1_000, "Catch-up yield changed event ordering.");
    Assert(pump.Snapshot.StateSeeks == 1, "Catch-up yield triggered an implicit state seek.");
  }

  private static void TestPreparedReplayDoesNotEmit()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(new List<RecordedInput> { Input(0, 40, true) });
    using (var pump = new ReplayNativeInputPump(scheduler, emitter))
    {
      Thread.Sleep(30);
      Assert(emitter.Snapshot().Count == 0, "Prepared pump emitted before it was armed.");
    }

    var context = new ActiveReplayContext { Phase = ReplayPlaybackPhase.Won };
    ReplayRunController.MarkRestartPrepared(context);
    Assert(context.Phase == ReplayPlaybackPhase.Prepared, "Won replay was not returned to Prepared on restart.");
  }

  private static void TestMiddleStartReplayInitializesFromPlayerControl()
  {
    var prepared = new ActiveReplayContext { Phase = ReplayPlaybackPhase.Prepared, RunStarted = false };
    Assert(
      ReplayRunController.ShouldInitializeFromPreRoll(prepared),
      "Prepared middle-start replay was not eligible for pre-roll initialization."
    );
    Assert(
      ReplayRunController.ShouldInitializeFromPlayerControl(prepared),
      "Prepared middle-start replay was not eligible for PlayerControl initialization."
    );

    prepared.RunStarted = true;
    Assert(
      !ReplayRunController.ShouldInitializeFromPlayerControl(prepared),
      "Running replay attempted PlayerControl initialization twice."
    );

    var armed = new ActiveReplayContext { Phase = ReplayPlaybackPhase.Armed, RunStarted = false };
    Assert(
      !ReplayRunController.ShouldInitializeFromPlayerControl(armed),
      "Countdown-armed replay incorrectly used the middle-start fallback."
    );
    Assert(
      !ReplayRunController.ShouldInitializeFromPreRoll(armed),
      "Pre-roll attempted to initialize an already armed replay."
    );
  }

  private static RecordedInput Input(long timeUs, int key, bool down)
  {
    RecordInputFlags flags = RecordInputFlags.Async;
    if (down)
      flags |= RecordInputFlags.Down;
    return new RecordedInput(timeUs, key, flags);
  }

  private sealed class CapturingEmitter : INativeInputEmitter
  {
    private readonly object _gate = new object();
    private readonly List<NativeInputEmission[]> _batches = new List<NativeInputEmission[]>();

    public bool IsSupported(int key) => key != 27;

    public NativeInputEmitResult EmitBatch(NativeInputEmission[] emissions, int offset, int count)
    {
      var copy = new NativeInputEmission[count];
      Array.Copy(emissions, offset, copy, 0, count);
      lock (_gate)
        _batches.Add(copy);
      return new NativeInputEmitResult(count);
    }

    public bool WaitForBatchCount(int count)
    {
      var timeout = System.Diagnostics.Stopwatch.StartNew();
      while (timeout.ElapsedMilliseconds < 1000)
      {
        lock (_gate)
        {
          if (_batches.Count >= count)
            return true;
        }
        Thread.Sleep(1);
      }
      return false;
    }

    public List<NativeInputEmission[]> Snapshot()
    {
      lock (_gate)
        return new List<NativeInputEmission[]>(_batches);
    }
  }

  private sealed class PartialEmitter : INativeInputEmitter
  {
    private readonly object _gate = new object();
    private readonly int _firstEmissionCount;
    private readonly int _retryEmissionCount;
    private int _calls;
    public readonly List<int> Offsets = new List<int>();

    public PartialEmitter(int firstEmissionCount, int retryEmissionCount)
    {
      _firstEmissionCount = firstEmissionCount;
      _retryEmissionCount = retryEmissionCount;
    }

    public bool IsSupported(int key) => true;

    public NativeInputEmitResult EmitBatch(NativeInputEmission[] emissions, int offset, int count)
    {
      lock (_gate)
      {
        Offsets.Add(offset);
        int allowed = _calls++ == 0 ? _firstEmissionCount : _retryEmissionCount;
        return new NativeInputEmitResult(Math.Min(count, allowed), allowed >= count ? 0 : 5);
      }
    }

    public bool WaitForCallCount(int count)
    {
      var timeout = System.Diagnostics.Stopwatch.StartNew();
      while (timeout.ElapsedMilliseconds < 1000)
      {
        lock (_gate)
        {
          if (_calls >= count)
            return true;
        }
        Thread.Sleep(1);
      }
      return false;
    }
  }
}
