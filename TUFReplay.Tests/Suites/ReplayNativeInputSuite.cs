using System.Reflection;
using Microsoft.Data.Sqlite;
using SkyHook;
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
    TestReplayTimelineTimeMath();
    TestReplayTimelineTopGlowPolicy();
    TestReplayTimelineJudgmentMapping();
    TestReplayTimelineLegacyJudgmentTimeMath();
    TestLegacyJudgmentOverloadMath();
    TestHitContextPlaybackPositionComparison();
    TestReplayNoFailPolicy();
    TestNativeInputUmmWindowInterlock();
    TestWindowsSkyHookRawKeyPreservation();
    TestWindowsPhysicalStateUsesCurrentDownBit();
    TestLegacyWindowsInitialStateRemoval();
    TestNativeInputMigrationCompatibility();
    TestWindowsPhysicalKeyMetadata();
    TestReplaySchedulerChord();
    TestReplayPumpTimingAndBatching();
    TestReplayPumpFocusAndReleaseAll();
    TestReplayPumpPauseSuspendsAndResumes();
    TestReplayPumpClockJumpSeeksState();
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

  private static void TestNativeInputMigrationCompatibility()
  {
    byte[] original = System.Text.Encoding.UTF8.GetBytes("1,69,3\n2,160,2\n");
    Assert(
      SkyHookInputKeyMigration.TryConvertInputCsv(original, out byte[] preserved, out int count, out int dropped),
      "Native input migration rejected a valid format-2 payload."
    );
    Assert(count == 2 && dropped == 0, "Native input migration changed the input count.");
    Assert(
      System.Text.Encoding.UTF8.GetString(preserved) == "1,69,3\n2,160,2\n",
      "Native input migration reinterpreted OS-native key codes as HID usages."
    );

    Assert(
      NativeInputKeyCodeMapper.TryConvertSkyHookHidUsage(69, out int incorrectlyMigratedF12),
      "Test setup could not reproduce the historical HID conversion."
    );
    var corruptedMeta = new ReplayMetadata
    {
      formatVersion = 3,
      inputKeySpace = NativeInputKeyCodeMapper.NativeKeySpace,
      inputCapture = NativeInputKeyCodeMapper.CorruptedNativeStateMigrationCapture,
    };
    var corrupted = new List<RecordedInput>
    {
      new RecordedInput(1, incorrectlyMigratedF12, RecordInputFlags.Async | RecordInputFlags.Down),
    };
    List<RecordedInput> repaired = NativeInputKeyCodeMapper.NormalizeForPlayback(corrupted, corruptedMeta, out dropped);
    Assert(repaired.Count == 1 && repaired[0].Key == 69, "Historical E-to-F12 corruption was not repaired.");
    Assert(dropped == 0, "A uniquely reversible migrated key was dropped.");

    Assert(
      NativeInputKeyCodeMapper.TryConvertSkyHookHidUsage(49, out int ambiguousBackslash),
      "Test setup could not reproduce an ambiguous historical conversion."
    );
    corrupted[0] = new RecordedInput(1, ambiguousBackslash, RecordInputFlags.Async | RecordInputFlags.Down);
    repaired = NativeInputKeyCodeMapper.NormalizeForPlayback(corrupted, corruptedMeta, out dropped);
    Assert(repaired.Count == 0 && dropped == 1, "Ambiguous historical key corruption was replayed unsafely.");
  }

  private static void TestWindowsSkyHookRawKeyPreservation()
  {
    AssertWindowsCapture(0x45, KeyLabel.E, 0x45, false, "E");
    AssertWindowsCapture(0x5D, KeyLabel.Unknown, 0x5D, true, "Menu");
    AssertWindowsCapture(0x15, KeyLabel.Unknown, 0x15, false, "Hangul");
    AssertWindowsCapture(0x19, KeyLabel.Unknown, 0x19, false, "Hanja");
    AssertWindowsCapture(0x10, KeyLabel.LShift, 0xA0, false, "left Shift");
    AssertWindowsCapture(0x11, KeyLabel.RControl, 0xA3, true, "right Ctrl");
    AssertWindowsCapture(0x12, KeyLabel.RAlt, 0xA5, true, "right Alt");
  }

  private static void TestWindowsPhysicalStateUsesCurrentDownBit()
  {
    Assert(
      WindowsNativeInputStateReader.IsAsyncKeyDown(unchecked((short)0x8000)),
      "Windows physical state ignored the current-down bit."
    );
    Assert(
      !WindowsNativeInputStateReader.IsAsyncKeyDown(0x0001),
      "Windows physical state treated the recent-press bit as currently down."
    );
    Assert(!WindowsNativeInputStateReader.IsAsyncKeyDown(0), "Windows physical state reported an idle key as down.");
  }

  private static void TestLegacyWindowsInitialStateRemoval()
  {
    RecordInputFlags down = RecordInputFlags.Async | RecordInputFlags.Down;
    var inputs = new List<RecordedInput>
    {
      new RecordedInput(-877_752, 187, down),
      new RecordedInput(-877_752, 189, down),
      new RecordedInput(-877_752, 220, down),
      new RecordedInput(-836_688, 82, down),
      new RecordedInput(-820_165, 82, RecordInputFlags.Async),
    };
    var legacyMeta = new ReplayMetadata
    {
      formatVersion = 3,
      inputKeySpace = NativeInputKeyCodeMapper.NativeKeySpace,
      inputCapture = NativeInputKeyCodeMapper.LegacyWindowsThreadStateCapture,
      inputNativePlatform = "windows",
    };

    List<RecordedInput> normalized = NativeInputKeyCodeMapper.NormalizeForPlayback(inputs, legacyMeta, out int dropped);
    Assert(dropped == 3, "Legacy Windows initial state group was not removed.");
    Assert(normalized.Count == 2 && normalized[0].Key == 82, "Real countdown input was removed with legacy state.");

    legacyMeta.inputCapture = NativeInputKeyCodeMapper.PhysicalStateCapture;
    normalized = NativeInputKeyCodeMapper.NormalizeForPlayback(inputs, legacyMeta, out dropped);
    Assert(dropped == 0 && normalized.Count == inputs.Count, "Physical-state recording was sanitized as legacy data.");
  }

  private static void AssertWindowsCapture(
    int rawVirtualKey,
    KeyLabel label,
    int expectedVirtualKey,
    bool expectedExtended,
    string name
  )
  {
    Assert(
      SkyHookNativeInputEventSource.TryResolveWindowsNativeKey(
        rawVirtualKey,
        label,
        out int actualVirtualKey,
        out bool actualExtended
      ),
      "Windows capture rejected " + name + "."
    );
    Assert(actualVirtualKey == expectedVirtualKey, "Windows capture remapped " + name + " to another key.");
    Assert(actualExtended == expectedExtended, "Windows capture assigned the wrong extended state to " + name + ".");
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

  private static void TestReplayPumpClockJumpSeeksState()
  {
    var emitter = new CapturingEmitter();
    var scheduler = new ReplayInputScheduler(
      new List<RecordedInput> { Input(0, 30, true), Input(10_000, 30, false), Input(20_000, 31, true) }
    );
    using var pump = new ReplayNativeInputPump(scheduler, emitter);

    pump.ResetTo(-1_000_000, 1d, true);
    pump.Synchronize(1_000_000, 1d, true);
    Assert(emitter.WaitForBatchCount(1), "Clock jump did not seek to final held state.");
    List<NativeInputEmission[]> batches = emitter.Snapshot();
    Assert(batches.Count == 1, "Clock jump emitted stale backlog.");
    Assert(batches[0].Length == 1 && batches[0][0].Key == 31 && batches[0][0].Down, "Clock seek held state is wrong.");
    Assert(pump.Snapshot.StateSeeks >= 2, "Clock jump state seek was not counted.");
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

    public bool EmitBatch(NativeInputEmission[] emissions, int count)
    {
      var copy = new NativeInputEmission[count];
      Array.Copy(emissions, copy, count);
      lock (_gate)
        _batches.Add(copy);
      return true;
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
}
