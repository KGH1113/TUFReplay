using System.Reflection;
using Microsoft.Data.Sqlite;
using SkyHook;
using TUFReplay;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Calibration;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Recording;
using TUFReplay.Application.Replay;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
using TUFReplay.Infrastructure.Adofai;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Database.Schema;
using TUFReplay.Infrastructure.NativeInput;
using TUFReplay.Infrastructure.NativeInput.Capture;
using TUFReplay.Infrastructure.Unity;

internal static class Program
{
  private static int Main()
  {
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      NativeSqliteLoader.Initialize();
      TestMicrophoneCaptureChunking();
      TestPendingMicrophoneDisposition();
      TestHitMarginSnapshotReuse();
      TestWavWriter(root);
      TestPlaybackWaveReader(root);
      TestPlaybackLimiter(root);
      TestReplayMicrophoneClock();
      TestCalibrationSettings(root);
      TestCalibrationWaveforms(root);
      TestGameplayChartHashVersioning();
      TestGameplayHashIdentityUpgrade(root);
      TestGameplayHashV3Migration(root);
      TestSchemaMigrationAndBlob(root);
      TestAppSessionTransientLockRecovery(root);
      TestBrokenRenamedForeignKeyRepair(root);
      TestRunDeletionHierarchy(root);
      TestLogicalRunSessionFilter(root);
      TestLogicalLevelIdentity(root);
      TestReplayInputStableOrder();
      TestReplayCsvParserCompatibility();
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
      TestReplayPumpClockJumpSeeksState();
      TestPreparedReplayDoesNotEmit();
      TestMiddleStartReplayInitializesFromPlayerControl();
      UpdaterTests.RunAll();
      Console.WriteLine("TUFReplay C# tests passed.");
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
    finally
    {
      Directory.Delete(root, true);
    }
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
    byte[] inputCsv = System.Text.Encoding.UTF8.GetBytes(
      "\r\n 200 , 11 , 3 \r\ninvalid\n100,7,2\r\n200,12,1\n"
    );
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
    Assert(
      NativeInputUmmWindowInterlock.ShouldPollManagerWindowAt(10_000L),
      "UMM fallback did not poll immediately."
    );
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

  private static void TestMicrophoneCaptureChunking()
  {
    int chunkFrames = UnityMicrophoneCaptureBackend.CaptureChunkFrames;
    int clipFrames = 48000 * 10;

    Assert(chunkFrames == 12000, "Microphone capture chunk duration changed unexpectedly.");
    Assert(
      UnityMicrophoneCaptureBackend.WriterQueueCapacity > clipFrames / chunkFrames,
      "Microphone writer queue cannot absorb a full loop-buffer backlog."
    );
    Assert(
      MicrophoneCaptureChunking.AvailableFrames(clipFrames - 1000, 1000, clipFrames) == 2000,
      "Microphone capture wraparound distance is incorrect."
    );
    Assert(
      MicrophoneCaptureChunking.NextChunkFrames(chunkFrames * 8, chunkFrames, false) == chunkFrames,
      "A capture hitch expanded the reusable read chunk."
    );
    Assert(
      MicrophoneCaptureChunking.NextChunkFrames(800, chunkFrames, false) == 0,
      "A partial live chunk was read before it was full."
    );
    Assert(
      MicrophoneCaptureChunking.NextChunkFrames(800, chunkFrames, true) == 800,
      "The final microphone tail was not drained."
    );
    Assert(
      MicrophoneCaptureChunking.AdvanceCursor(clipFrames - 1000, 2000, clipFrames) == 1000,
      "Microphone capture cursor did not wrap correctly."
    );
  }

  private static void TestPendingMicrophoneDisposition()
  {
    var recording = new CapturedMicrophoneRecording { RunId = "run" };
    int completed = 0;
    CapturedMicrophoneRecording observed = null;
    bool persisted = false;
    var disposition = new PendingMicrophoneDisposition((value, shouldPersist) =>
    {
      completed++;
      observed = value;
      persisted = shouldPersist;
    });

    disposition.CompleteDisposition(persist: true);
    Assert(completed == 0, "Microphone disposition ran before capture finalization.");
    disposition.CompleteCapture(recording);
    Assert(completed == 1, "Microphone disposition did not run after both gates completed.");
    Assert(ReferenceEquals(observed, recording) && persisted, "Microphone persistence decision was lost.");
    disposition.CompleteDisposition(persist: false);
    disposition.CompleteCapture(recording);
    Assert(completed == 1, "Microphone disposition completed more than once.");

    completed = 0;
    var captureFirst = new PendingMicrophoneDisposition((_, shouldPersist) =>
    {
      completed++;
      persisted = shouldPersist;
    });
    captureFirst.CompleteCapture(recording);
    Assert(completed == 0, "Finalized microphone capture ignored the editor-return gate.");
    captureFirst.CompleteDisposition(persist: false);
    Assert(completed == 1 && !persisted, "Deferred microphone discard decision was lost.");
  }

  private static void TestHitMarginSnapshotReuse()
  {
    var snapshot = new HitMarginSnapshot();
    int[] firstMargins = { 1, 2, 3 };

    Assert(!snapshot.Matches(firstMargins), "An empty hit margin snapshot matched values.");
    snapshot.Capture(firstMargins);
    int[] initialBuffer = snapshot.BufferForTesting;

    Assert(snapshot.Matches(new[] { 1, 2, 3 }), "Captured hit margins did not match.");
    Assert(!snapshot.Matches(new[] { 1, 2, 4 }), "Different hit margins matched the snapshot.");

    snapshot.Capture(new[] { 4, 5, 6 });
    Assert(
      ReferenceEquals(initialBuffer, snapshot.BufferForTesting),
      "Capturing same-length hit margins allocated a new buffer."
    );
    Assert(snapshot.Matches(new[] { 4, 5, 6 }), "Reused hit margin buffer was not updated.");

    snapshot.Reset();
    Assert(!snapshot.Matches(new[] { 4, 5, 6 }), "Reset hit margin snapshot remained valid.");
    snapshot.Capture(new[] { 7, 8, 9 });
    Assert(
      ReferenceEquals(initialBuffer, snapshot.BufferForTesting),
      "Reset discarded the reusable hit margin buffer."
    );

    snapshot.Capture(new[] { 1, 2 });
    Assert(
      !ReferenceEquals(initialBuffer, snapshot.BufferForTesting),
      "A changed hit margin count did not resize the snapshot buffer."
    );
  }

  private static void TestWavWriter(string root)
  {
    string path = Path.Combine(root, "writer.wav");
    using (var writer = new Pcm16WavWriter(path, 2))
    {
      float[] stereo = { 2f, -2f, 2f, 2f, -2f, -2f, 0.5f, 0.5f };
      Assert(writer.TryEnqueue(stereo, stereo.Length, 2), "WAV chunk was not queued.");
      Assert(writer.Complete() == 4, "WAV frame count is incorrect.");
    }

    byte[] wav = File.ReadAllBytes(path);
    Assert(System.Text.Encoding.ASCII.GetString(wav, 0, 4) == "RIFF", "RIFF header is missing.");
    Assert(System.Text.Encoding.ASCII.GetString(wav, 8, 4) == "WAVE", "WAVE header is missing.");
    Assert(BitConverter.ToInt32(wav, 40) == 8, "PCM byte length is incorrect.");
    Assert(BitConverter.ToInt16(wav, 44) == 0, "Stereo downmix is incorrect.");
    Assert(BitConverter.ToInt16(wav, 46) == short.MaxValue, "Positive clipping is incorrect.");
    Assert(BitConverter.ToInt16(wav, 48) == short.MinValue, "Negative clipping is incorrect.");
  }

  private static void TestPlaybackWaveReader(string root)
  {
    string sourcePath = Path.Combine(root, "playback-source.wav");
    using (var writer = new Pcm16WavWriter(sourcePath))
    {
      Assert(writer.TryEnqueue(new[] { 0f, 0.25f, -0.25f, 1f }, 4, 1), "Playback WAV was not queued.");
      Assert(writer.Complete() == 4, "Playback WAV frame count is incorrect.");
    }

    StoredMicrophoneRecording source = PlaybackRecording(sourcePath, 4);
    Pcm16WaveInfo info = Pcm16WaveFile.ReadAndValidate(source);
    Assert(info.DataOffset == 44 && info.DataLength == 8, "Playback WAV data location is incorrect.");

    byte[] original = File.ReadAllBytes(sourcePath);
    byte[] withUnknownChunk = new byte[original.Length + 10];
    Array.Copy(original, 0, withUnknownChunk, 0, 12);
    System.Text.Encoding.ASCII.GetBytes("JUNK").CopyTo(withUnknownChunk, 12);
    BitConverter.GetBytes(1).CopyTo(withUnknownChunk, 16);
    withUnknownChunk[20] = 0x7f;
    Array.Copy(original, 12, withUnknownChunk, 22, original.Length - 12);
    BitConverter.GetBytes(withUnknownChunk.Length - 8).CopyTo(withUnknownChunk, 4);
    string unknownChunkPath = Path.Combine(root, "playback-unknown-chunk.wav");
    File.WriteAllBytes(unknownChunkPath, withUnknownChunk);
    info = Pcm16WaveFile.ReadAndValidate(PlaybackRecording(unknownChunkPath, 4));
    Assert(info.DataLength == 8, "Playback WAV reader did not skip an unknown chunk.");

    StoredMicrophoneRecording mismatch = PlaybackRecording(sourcePath, 4);
    mismatch.SampleRate = 44100;
    AssertThrows<InvalidDataException>(
      () => Pcm16WaveFile.ReadAndValidate(mismatch),
      "Playback WAV metadata mismatch was accepted."
    );

    string truncatedPath = Path.Combine(root, "playback-truncated.wav");
    File.WriteAllBytes(truncatedPath, new byte[8]);
    AssertThrows<InvalidDataException>(
      () => Pcm16WaveFile.ReadAndValidate(PlaybackRecording(truncatedPath, 0)),
      "Truncated playback WAV was accepted."
    );

    byte[] floatFormat = (byte[])original.Clone();
    floatFormat[20] = 3;
    string floatPath = Path.Combine(root, "playback-float.wav");
    File.WriteAllBytes(floatPath, floatFormat);
    AssertThrows<InvalidDataException>(
      () => Pcm16WaveFile.ReadAndValidate(PlaybackRecording(floatPath, 4)),
      "Non-PCM16 playback WAV was accepted."
    );
  }

  private static void TestPlaybackLimiter(string root)
  {
    const int sampleRate = 48000;
    const int frameCount = sampleRate / 2;
    int transientFrame = sampleRate / 10;
    var samples = new float[frameCount];
    Array.Fill(samples, 0.01f);
    samples[transientFrame] = 1f;

    string path = Path.Combine(root, "playback-limiter.wav");
    using (var writer = new Pcm16WavWriter(path))
    {
      Assert(writer.TryEnqueue(samples, samples.Length, 1), "Limiter WAV was not queued.");
      Assert(writer.Complete() == frameCount, "Limiter WAV frame count is incorrect.");
    }

    StoredMicrophoneRecording recording = PlaybackRecording(path, frameCount);
    Pcm16WaveInfo wave = Pcm16WaveFile.ReadAndValidate(recording);
    Pcm16LimiterEnvelope envelope = Pcm16WaveAnalyzer.Analyze(recording, wave, System.Threading.CancellationToken.None);
    Assert(envelope.BinCount == 500, "Limiter envelope did not use one-millisecond bins.");
    Assert(Math.Abs(envelope.RequiredLimiterGain(0, 10f) - 1f) < 0.0001f, "Limiter attenuated a quiet section.");
    Assert(
      envelope.RequiredLimiterGain(transientFrame - sampleRate * 4 / 1000, 10f) < 1f,
      "Limiter look-ahead did not anticipate a loud transient."
    );
    Assert(
      Math.Abs(envelope.RequiredLimiterGain(transientFrame - sampleRate * 7 / 1000, 10f) - 1f) < 0.0001f,
      "Limiter look-ahead started too early."
    );

    float limitedGain = envelope.RequiredLimiterGain(transientFrame, 10f) * 10f;
    Assert(limitedGain <= Pcm16LimiterEnvelope.Ceiling + 0.0001f, "Limiter exceeded its true-peak ceiling.");

    var limiter = new Pcm16Limiter(envelope, sampleRate);
    float peakGain = 10f;
    for (int frame = transientFrame - sampleRate * 7 / 1000; frame <= transientFrame; frame++)
      peakGain = limiter.NextEffectiveGain(frame, 10f);
    float releaseGain = peakGain;
    for (int frame = transientFrame + 1; frame <= transientFrame + sampleRate / 10; frame++)
      releaseGain = limiter.NextEffectiveGain(frame, 10f);
    Assert(releaseGain > peakGain && releaseGain < 10f, "Limiter release did not recover smoothly.");

    limiter.Reset();
    float resetPeakGain = limiter.NextEffectiveGain(transientFrame, 10f);
    Assert(Math.Abs(resetPeakGain - limitedGain) < 0.0001f, "Limiter reset did not seek by absolute PCM frame.");
    limiter.Reset();
    Assert(Math.Abs(limiter.NextEffectiveGain(0, 1f) - 1f) < 0.0001f, "A safe gain change was unnecessarily limited.");

    string stereoPath = Path.Combine(root, "playback-limiter-stereo.wav");
    short[] stereoSamples = new short[32];
    stereoSamples[16] = short.MaxValue;
    stereoSamples[17] = short.MaxValue / 4;
    WriteTestPcm16Wave(stereoPath, sampleRate, 2, stereoSamples);
    StoredMicrophoneRecording stereoRecording = PlaybackRecording(stereoPath, 16, sampleRate, 2);
    Pcm16WaveInfo stereoWave = Pcm16WaveFile.ReadAndValidate(stereoRecording);
    Pcm16LimiterEnvelope stereoEnvelope = Pcm16WaveAnalyzer.Analyze(
      stereoRecording,
      stereoWave,
      System.Threading.CancellationToken.None
    );
    Assert(
      stereoEnvelope.RequiredLimiterGain(8, 10f) < 0.1f,
      "Limiter did not link channels using the loudest channel."
    );

    using var cancelled = new System.Threading.CancellationTokenSource();
    cancelled.Cancel();
    AssertThrows<OperationCanceledException>(
      () => Pcm16WaveAnalyzer.Analyze(recording, wave, cancelled.Token),
      "Cancelled limiter analysis completed."
    );
  }

  private static void TestReplayMicrophoneClock()
  {
    Assert(
      ReplayMicrophoneClock.ApplyLatencyCorrection(-1_000_000L, 100_000L) == -1_100_000L,
      "Positive microphone latency did not advance playback."
    );
    Assert(
      ReplayMicrophoneClock.ApplyLatencyCorrection(-1_000_000L, -100_000L) == -900_000L,
      "Negative microphone latency did not delay playback."
    );
    Assert(ReplayMicrophoneClock.ToFrame(500_000, 1d, 0L, 48000, 100000) == 24000, "100% mic clock is wrong.");
    Assert(ReplayMicrophoneClock.ToFrame(500_000, 2d, 0L, 48000, 100000) == 12000, "Pitched mic clock is wrong.");
    Assert(
      ReplayMicrophoneClock.ToFrame(500_000, 1d, 100_000L, 48000, 100000) == 19200,
      "Mic capture offset is wrong."
    );
    Assert(ReplayMicrophoneClock.ToFrame(50_000, 1d, 100_000L, 48000, 100000) == 0, "Pre-roll was not clamped.");
    Assert(ReplayMicrophoneClock.ToFrame(5_000_000, 1d, 0L, 48000, 1000) == 1000, "Mic end was not clamped.");
    Assert(ReplayMicrophoneClock.ToFrame(500_000, 0d, 0L, 48000, 100000) == 24000, "Invalid pitch fallback is wrong.");
    Assert(
      ReplayMicrophoneClock.ToFrame(-500_000, 1d, -1_000_000L, 48000, 100000) == 24000,
      "Countdown microphone pre-roll is wrong."
    );
    Assert(
      ReplayMicrophoneClock.ToFrame(0L, 2d, -1_000_000L, 48000, 100000) == 48000,
      "Pitched gameplay start did not preserve microphone pre-roll."
    );
    Assert(
      ReplayMicrophoneClock.ToFrame(2_000_000L, 2d, -1_000_000L, 48000, 200000, 2_000_000L) == 96000,
      "Won transition microphone frame is wrong."
    );
    Assert(
      ReplayMicrophoneClock.ToFrame(2_500_000L, 2d, -1_000_000L, 48000, 200000, 2_000_000L) == 120000,
      "Won microphone clock was not continuous."
    );
    Assert(
      ReplayMicrophoneClock.ToFrame(0L, 1d, -900_000L, 48000, 100000) == 43200,
      "User offset and microphone pre-roll composition is wrong."
    );
  }

  private static void TestCalibrationSettings(string root)
  {
    string legacyPath = Path.Combine(root, "legacy-settings.json");
    File.WriteAllText(legacyPath, "{\"Setting\":{\"AutoRecord\":false}}");
    TUFReplaySetting legacy = TUFReplaySetting.Load(legacyPath);
    Assert(!legacy.AutoRecord, "Legacy AutoRecord setting was not loaded.");
    Assert(legacy.MicrophoneEnabled, "Legacy microphone access did not default to enabled.");
    Assert(legacy.MicrophoneOffsetMs == 0, "Legacy calibration offset default is incorrect.");
    Assert(legacy.MicrophoneVolumeDb == 0, "Legacy calibration volume default is incorrect.");

    string offsetPath = Path.Combine(root, "legacy-offset-settings.json");
    File.WriteAllText(offsetPath, "{\"Setting\":{\"MicrophoneOffsetMs\":-80}}");
    TUFReplaySetting migratedOffset = TUFReplaySetting.Load(offsetPath);
    Assert(migratedOffset.MicrophoneOffsetMs == 80, "Legacy microphone offset sign was not migrated.");
    Assert(
      migratedOffset.MicrophoneOffsetConventionVersion == TUFReplaySetting.CurrentMicrophoneOffsetConventionVersion,
      "Microphone offset convention version was not migrated."
    );
    migratedOffset.Save(offsetPath);
    Assert(TUFReplaySetting.Load(offsetPath).MicrophoneOffsetMs == 80, "Microphone offset sign migrated twice.");

    string percentPath = Path.Combine(root, "percent-settings.json");
    File.WriteAllText(percentPath, "{\"Setting\":{\"MicrophoneVolumePercent\":200}}");
    Assert(TUFReplaySetting.Load(percentPath).MicrophoneVolumeDb == 6, "Legacy percent volume was not migrated.");

    string disabledPath = Path.Combine(root, "disabled-microphone-settings.json");
    var disabled = new TUFReplaySetting { MicrophoneEnabled = false, MicrophoneDeviceId = "saved-device" };
    disabled.Save(disabledPath);
    TUFReplaySetting reloadedDisabled = TUFReplaySetting.Load(disabledPath);
    Assert(!reloadedDisabled.MicrophoneEnabled, "Disabled microphone access was not persisted.");
    Assert(reloadedDisabled.MicrophoneDeviceId == "saved-device", "Disabled microphone access lost its device.");

    legacy.MicrophoneOffsetMs = 999;
    legacy.MicrophoneVolumeDb = -50;
    legacy.Normalize();
    Assert(legacy.MicrophoneOffsetMs == TUFReplaySetting.MaxMicrophoneOffsetMs, "Calibration offset was not clamped.");
    Assert(legacy.MicrophoneVolumeDb == TUFReplaySetting.MinMicrophoneVolumeDb, "Calibration volume was not clamped.");
    Assert(Math.Abs(MicrophoneGain.FromDecibels(-20) - 0.1f) < 0.0001f, "-20 dB gain is incorrect.");
    Assert(Math.Abs(MicrophoneGain.FromDecibels(0) - 1f) < 0.0001f, "0 dB gain is incorrect.");
    Assert(Math.Abs(MicrophoneGain.FromDecibels(20) - 10f) < 0.0001f, "+20 dB gain is incorrect.");
  }

  private static void TestCalibrationWaveforms(string root)
  {
    float[] inputs = CalibrationWaveformBuilder.FromInputEvents(
      new List<RecordedInput>
      {
        new RecordedInput(-50_000L, 1, RecordInputFlags.Down | RecordInputFlags.Async),
        new RecordedInput(250_000L, 1, RecordInputFlags.Down | RecordInputFlags.Async),
        new RecordedInput(260_000L, 1, RecordInputFlags.Async),
      },
      1000d
    );
    Assert(inputs[CalibrationWaveformBuilder.BinCount / 4] == 1f, "Input waveform missed a key-down timestamp.");
    Assert(inputs[0] == 0f, "Input waveform included a countdown key press.");
    Assert(CalibrationWaveformBuilder.HasSignal(inputs), "Input waveform signal detection failed.");
    Assert(
      !CalibrationWaveformBuilder.HasSignal(
        CalibrationWaveformBuilder.FromInputEvents(
          new List<RecordedInput> { new RecordedInput(250_000L, 1, RecordInputFlags.Async) },
          1000d
        )
      ),
      "Input waveform included a key-up timestamp."
    );

    string path = Path.Combine(root, "calibration-waveform.wav");
    using (var writer = new Pcm16WavWriter(path))
    {
      Assert(writer.TryEnqueue(new[] { 0.25f, -1f, 0.5f, 0f }, 4, 1), "Calibration WAV was not queued.");
      Assert(writer.Complete() == 4, "Calibration WAV frame count is incorrect.");
    }
    var recording = new CapturedMicrophoneRecording
    {
      RunId = "calibration",
      TempPath = path,
      SampleRate = 48000,
      Channels = 1,
      FrameCount = 4,
      CaptureStartOffsetUs = 500_000,
    };
    float[] microphone = CalibrationWaveformBuilder.FromPcm16(recording, 1000d);
    Assert(microphone.Length == CalibrationWaveformBuilder.BinCount, "Microphone waveform bin count is wrong.");
    Assert(
      microphone[CalibrationWaveformBuilder.BinCount / 2] > 0.99f,
      "Microphone waveform did not apply the capture start offset."
    );
    Assert(microphone[0] == 0f, "Microphone waveform leaked before its capture start.");

    float[] game = CalibrationWaveformBuilder.FromTimedPeaks(
      new[] { 0.5f, 1f },
      new long[] { 100, 600 },
      2,
      1000,
      1000d
    );
    Assert(game.Length == CalibrationWaveformBuilder.BinCount, "Game waveform bin count is wrong.");
    Assert(
      Math.Abs(game[CalibrationWaveformBuilder.BinCount / 20] - 0.5f) < 0.001f,
      "Game waveform peak normalization is wrong."
    );
    Assert(
      game[CalibrationWaveformBuilder.BinCount / 2] > 0.99f && game[CalibrationWaveformBuilder.BinCount * 4 / 5] == 0f,
      "Game waveform timing aggregation is wrong."
    );

    string referencePath = Path.Combine(root, "calibration-reference.waveform");
    using (var stream = new FileStream(referencePath, FileMode.Create, FileAccess.Write, FileShare.None))
    using (var writer = new BinaryWriter(stream))
    {
      writer.Write(System.Text.Encoding.ASCII.GetBytes("TUFWRF1\0"));
      writer.Write((uint)1000);
      writer.Write((uint)1);
      writer.Write((uint)4);
      writer.Write((ushort)0);
      writer.Write((ushort)32768);
      writer.Write(ushort.MaxValue);
      writer.Write((ushort)0);
    }
    CalibrationReferenceWaveform reference = CalibrationReferenceWaveform.Read(referencePath);
    float[] referenceSong = CalibrationWaveformBuilder.FromReferenceWaveform(reference, 1, 1000, 3d);
    Assert(referenceSong[0] > 0.49f, "Reference song waveform did not apply its source start frame.");
    Assert(referenceSong[CalibrationWaveformBuilder.BinCount / 3] > 0.99f, "Reference song waveform timing is wrong.");
    float[] scheduledSong = CalibrationWaveformBuilder.FromReferenceWaveform(reference, -1, 1000, 5d);
    Assert(scheduledSong[0] == 0f, "Scheduled song waveform did not preserve leading silence.");
    Assert(
      scheduledSong[CalibrationWaveformBuilder.BinCount * 2 / 5] > 0.49f
        && scheduledSong[CalibrationWaveformBuilder.BinCount * 3 / 5] > 0.99f,
      "Scheduled song waveform did not shift source peaks onto the gameplay timeline."
    );
    Assert(
      CalibrationWaveformBuilder.SourceFrameAtDspTime(9.5d, 8d, true, 0.5d, 4d, 1d, 1000) == -500,
      "Scheduled calibration audio did not preserve its leading silence."
    );
    Assert(
      CalibrationWaveformBuilder.SourceFrameAtDspTime(10d, 8d, true, 0.5d, 4d, 1d, 1000) == 0,
      "Scheduled calibration audio did not start at source frame zero."
    );
    Assert(
      CalibrationWaveformBuilder.SourceFrameAtDspTime(10d, 8d, false, 0.5d, 4d, 2d, 1000) == 4000,
      "Pitched source frame calculation is wrong."
    );
  }

  private static void TestSchemaMigrationAndBlob(string root)
  {
    string path = Path.Combine(root, "activity.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand version = connection.CreateCommand();
      version.CommandText = "PRAGMA user_version";
      Assert(Convert.ToInt32(version.ExecuteScalar()) == ActivitySchema.Version, "Fresh schema version is incorrect.");
      InsertRun(connection);
    }

    StoredReplayRun replayRun = RunRepository.GetReplayRun("run");
    Assert(
      replayRun?.JudgmentDifficulty == RunJudgmentDifficulty.Normal,
      "Replay run did not preserve its judgment difficulty."
    );
    Assert(replayRun.NoFailMode, "Replay run did not preserve its No-Fail mode.");

    LogicalLevelOverview logicalLevel = ActivityRepository.GetLogicalLevelOverview("logical");
    Assert(logicalLevel != null, "Logical level overview was not available from the current schema.");
    Assert(logicalLevel.LevelTileCount == 0, "Logical level overview did not read the level tile count.");
    Assert(logicalLevel.VisitCount == 1, "Logical level overview did not count its level session.");
    Assert(logicalLevel.RunCount == 1, "Logical level overview did not count its run.");

    string wavPath = Path.Combine(root, "blob.wav.save-pending");
    using (var writer = new Pcm16WavWriter(wavPath))
    {
      Assert(writer.TryEnqueue(new[] { 0f, 0.25f, -0.25f }, 3, 1), "BLOB WAV chunk was not queued.");
      writer.Complete();
    }
    var recording = new CapturedMicrophoneRecording
    {
      RunId = "run",
      TempPath = wavPath,
      SampleRate = 48000,
      Channels = 1,
      FrameCount = 3,
      DeviceId = "test-device",
      CaptureStartOffsetUs = 123,
    };
    DateTime retentionSavedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    MicrophoneRecordingRepository.Save(recording, retentionSavedAt);

    string playbackPath = Path.Combine(root, "blob-playback.wav");
    StoredMicrophoneRecording copied = MicrophoneRecordingRepository.CopyForPlayback(
      "run",
      playbackPath,
      System.Threading.CancellationToken.None
    );
    Assert(copied != null, "Microphone BLOB was not available for playback.");
    Assert(
      File.ReadAllBytes(playbackPath).SequenceEqual(File.ReadAllBytes(wavPath)),
      "Playback BLOB copy changed bytes."
    );
    Pcm16WaveInfo copiedWave = Pcm16WaveFile.ReadAndValidate(copied);
    Assert(copiedWave.FrameCount == 3, "Copied playback WAV metadata is incorrect.");
    Assert(
      MicrophoneRecordingRepository.CopyForPlayback(
        "missing-run",
        Path.Combine(root, "missing.wav"),
        System.Threading.CancellationToken.None
      ) == null,
      "Missing microphone recording did not return null."
    );
    Assert(
      MicrophoneRecordingRepository.DeleteExpired(retentionSavedAt.AddDays(2)) == 0,
      "Temporary microphone recording expired too early."
    );
    Assert(MicrophoneRecordingRepository.KeepPermanently("run"), "Microphone recording was not kept permanently.");
    Assert(
      !MicrophoneRecordingRepository.KeepPermanently("run"),
      "Repeated permanent microphone retention was not idempotent."
    );
    Assert(
      MicrophoneRecordingRepository.DeleteExpired(retentionSavedAt.AddDays(30)) == 0,
      "Permanent microphone recording expired."
    );
    Assert(MicrophoneRecordingRepository.Delete("run"), "Microphone recording was not deleted.");
    Assert(!MicrophoneRecordingRepository.Delete("run"), "Repeated microphone recording deletion was not idempotent.");
    Assert(MicrophoneRecordingRepository.RunExists("run"), "Microphone recording deletion removed its run.");
    Assert(
      MicrophoneRecordingRepository.CopyForPlayback(
        "run",
        Path.Combine(root, "deleted.wav"),
        System.Threading.CancellationToken.None
      ) == null,
      "Deleted microphone recording remained available for playback."
    );
    MicrophoneRecordingRepository.Save(recording, retentionSavedAt);
    Assert(
      MicrophoneRecordingRepository.DeleteExpired(retentionSavedAt.AddDays(3)) == 1,
      "Temporary microphone recording did not expire after three days."
    );
    Assert(MicrophoneRecordingRepository.RunExists("run"), "Expiration removed the microphone recording's run.");
    MicrophoneRecordingRepository.Save(recording);

    using (var cancelled = new System.Threading.CancellationTokenSource())
    {
      cancelled.Cancel();
      string cancelledPath = Path.Combine(root, "cancelled.wav");
      AssertThrows<OperationCanceledException>(
        () => MicrophoneRecordingRepository.CopyForPlayback("run", cancelledPath, cancelled.Token),
        "Cancelled microphone BLOB copy completed."
      );
      Assert(
        !File.Exists(cancelledPath) && !File.Exists(cancelledPath + ".copying"),
        "Cancelled BLOB copy leaked a file."
      );
    }

    using (SqliteConnection connection = MicrophoneDatabase.OpenConnection())
    {
      using SqliteCommand verify = connection.CreateCommand();
      verify.CommandText =
        "SELECT length(audio_wav),sample_rate,channels,frame_count,capture_start_offset_us FROM microphone_recordings WHERE run_id='run'";
      using SqliteDataReader reader = verify.ExecuteReader();
      Assert(reader.Read(), "Microphone recording row is missing.");
      Assert(reader.GetInt64(0) == new FileInfo(wavPath).Length, "Incremental BLOB length is incorrect.");
      Assert(
        reader.GetInt32(1) == 48000 && reader.GetInt32(2) == 1 && reader.GetInt64(3) == 3,
        "BLOB metadata is incorrect."
      );
    }

    RunRecord runWithMicrophone = RunRepository.Get("run");
    Assert(
      runWithMicrophone?.MicrophoneRecordingBytes == new FileInfo(wavPath).Length,
      "Run query did not hydrate microphone metadata from the separate database."
    );
    List<RunRecord> metadataPage = Enumerable
      .Range(0, 1000)
      .Select(index => new RunRecord { Id = "missing-" + index })
      .ToList();
    metadataPage.Add(new RunRecord { Id = "run" });
    MicrophoneRecordingRepository.PopulateMetadata(metadataPage);
    Assert(
      metadataPage[metadataPage.Count - 1].MicrophoneRecordingBytes == new FileInfo(wavPath).Length,
      "Large run pages did not batch microphone metadata lookups."
    );

    using (SqliteConnection audioLock = MicrophoneDatabase.OpenConnection())
    using (SqliteTransaction audioTransaction = audioLock.BeginTransaction())
    {
      using SqliteCommand holdAudioWriter = audioLock.CreateCommand();
      holdAudioWriter.Transaction = audioTransaction;
      holdAudioWriter.CommandText = "UPDATE microphone_recordings SET expires_at_utc=expires_at_utc WHERE run_id='run'";
      holdAudioWriter.ExecuteNonQuery();

      using SqliteConnection activityWrite = Database.OpenConnection();
      using SqliteCommand insertNextRun = activityWrite.CreateCommand();
      insertNextRun.CommandText =
        "INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result) VALUES('run-isolation','level',1,'2026-01-02',0,'failed')";
      insertNextRun.ExecuteNonQuery();
      audioTransaction.Rollback();
    }
    Assert(RunRepository.Exists("run-isolation"), "Audio writer lock blocked the next activity run write.");
    RunRepository.Delete("run-isolation");

    MicrophoneRecordingRepository.Delete("run");
    using (SqliteConnection connection = Database.OpenConnection())
    {
      using SqliteCommand legacy = connection.CreateCommand();
      legacy.CommandText =
        @"INSERT INTO microphone_recordings(
run_id,audio_wav,format,sample_rate,channels,frame_count,device_id,capture_start_offset_us,is_permanent,expires_at_utc
) VALUES('run',@audio,'wav/pcm16',48000,1,3,'legacy-device',123,1,NULL)";
      legacy.Parameters.AddWithValue("@audio", File.ReadAllBytes(wavPath));
      legacy.ExecuteNonQuery();
    }
    Assert(MicrophoneRecordingRepository.MigrateLegacyRecordings() == 1, "Legacy migration count is incorrect.");
    Assert(MicrophoneRecordingRepository.Exists("run"), "Legacy microphone recording was not migrated.");
    using (SqliteConnection connection = Database.OpenConnection())
    {
      using SqliteCommand remaining = connection.CreateCommand();
      remaining.CommandText = "SELECT count(*) FROM microphone_recordings";
      Assert(Convert.ToInt32(remaining.ExecuteScalar()) == 0, "Legacy microphone row was not retired.");
    }

    using (SqliteConnection connection = Database.OpenConnection())
    {
      using SqliteCommand delete = connection.CreateCommand();
      delete.CommandText = "DELETE FROM runs WHERE id='run'";
      delete.ExecuteNonQuery();
    }
    Assert(MicrophoneRecordingRepository.Exists("run"), "Orphan recovery test lost its microphone row early.");
    Assert(MicrophoneRecordingRepository.DeleteOrphans() == 1, "Orphan microphone recording was not removed.");

    TestLegacyLevelMigration(root, 11);
    TestLegacyLevelMigration(root, 12);
  }

  private static void TestGameplayChartHashVersioning()
  {
    byte[] v1Before;
    using (var writer = new GameplayChartHashCanonicalWriter())
    {
      writer.WriteAngles(new[] { 0f, 90f, 180f });
      v1Before = writer.ComputeMd5Hash();
    }

    byte[] v2Before = ComputeVersion2Hash(100f, "song.ogg", 100, 0, 100);
    byte[] v3Before = ComputeVersion3Hash(100f, "song.ogg", 100, 0, 100);
    Assert(
      v1Before.Length == 16 && v2Before.Length == 32 && v3Before.Length == 32,
      "Gameplay hash sizes are incorrect."
    );

    byte[] v2After = ComputeVersion2Hash(101f, "song.ogg", 100, 0, 100);
    Assert(GameplayChartHash.Equals(v1Before, v1Before), "Legacy v1 hash comparison failed.");
    Assert(!GameplayChartHash.Equals(v2Before, v2After), "Gameplay BPM did not change the v2 hash.");
    Assert(
      !GameplayChartHash.Equals(v2Before, ComputeVersion2Hash(100f, "other.ogg", 100, 0, 100)),
      "Gameplay audio did not change the v2 hash."
    );
    Assert(
      !GameplayChartHash.Equals(v2Before, ComputeVersion2Hash(100f, "song.ogg", 70, 1, 40)),
      "Legacy v2 playback presentation fields unexpectedly stopped affecting its hash."
    );
    Assert(
      !GameplayChartHash.Equals(v3Before, ComputeVersion3Hash(101f, "song.ogg", 100, 0, 100)),
      "Gameplay BPM did not change the v3 hash."
    );
    Assert(
      !GameplayChartHash.Equals(v3Before, ComputeVersion3Hash(100f, "other.ogg", 100, 0, 100)),
      "Gameplay audio did not change the v3 hash."
    );
    Assert(
      GameplayChartHash.Equals(v3Before, ComputeVersion3Hash(100f, "song.ogg", 70, 1, 40)),
      "Pitch or hit-sound presentation changed the v3 chart identity."
    );
    Assert(GameplayChartHash.IsSupported(1, v1Before), "Legacy v1 hash is not supported.");
    Assert(GameplayChartHash.IsSupported(2, v2Before), "Legacy v2 hash is not supported.");
    Assert(GameplayChartHash.IsSupported(3, v3Before), "Current v3 hash is not supported.");
  }

  private static void TestAppSessionTransientLockRecovery(string root)
  {
    string path = Path.Combine(root, "app-session-lock.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
      ActivitySchema.Ensure(connection);

    using var lockAcquired = new System.Threading.ManualResetEventSlim();
    System.Threading.Tasks.Task blocker = System.Threading.Tasks.Task.Run(() =>
    {
      using SqliteConnection connection = Database.OpenConnection();
      using SqliteCommand command = connection.CreateCommand();
      command.CommandText = "BEGIN IMMEDIATE;";
      command.ExecuteNonQuery();
      lockAcquired.Set();
      System.Threading.Thread.Sleep(2500);
      command.CommandText = "ROLLBACK;";
      command.ExecuteNonQuery();
    });

    Assert(lockAcquired.Wait(TimeSpan.FromSeconds(5)), "Test writer did not acquire the activity database lock.");
    var tracker = new RecordingActivityTracker();
    Assert(tracker.StartAppSession(), "App session did not recover from a transient SQLite write lock.");
    blocker.GetAwaiter().GetResult();
    Assert(tracker.AppSessionId != null, "Recovered app session did not publish its ID.");
    Assert(CountRows("app_sessions", "id='" + tracker.AppSessionId + "'") == 1, "Recovered app session was not saved.");
    tracker.StopAppSession();
  }

  private static void TestGameplayHashIdentityUpgrade(string root)
  {
    SetDatabasePath(Path.Combine(root, "gameplay-hash-identity-upgrade.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
      ActivitySchema.Ensure(connection);

    AppSessionRepository.Save(
      new AppSession
      {
        Id = "hash-upgrade-app",
        StartedAtUtc = "2026-01-01T00:00:00Z",
        RecorderUtcOffsetMinutes = 0,
      }
    );

    byte[] legacyHash = new byte[GameplayChartHash.Version2Size];
    legacyHash[0] = 2;
    var legacyLevel = new LevelRecord
    {
      Id = "legacy-hash-level",
      SourceKind = LevelSourceKind.Local,
      LevelPath = Path.Combine(root, "hash-upgrade.adofai"),
      GameplayHash = legacyHash,
      GameplayHashVersion = 2,
      FirstSeenAtUtc = "2026-01-01T00:00:00Z",
      LastSeenAtUtc = "2026-01-01T00:00:00Z",
    };
    string legacyLevelId = LevelRepository.ResolveOrCreate(legacyLevel);
    LevelSessionRepository.Save(
      new LevelSession
      {
        Id = "legacy-hash-session",
        LevelId = legacyLevelId,
        AppSessionId = "hash-upgrade-app",
        OpenedAtUtc = "2026-01-01T00:00:00Z",
      }
    );

    byte[] currentHash = new byte[GameplayChartHash.Version3Size];
    currentHash[0] = 3;
    var currentLevel = new LevelRecord
    {
      Id = "current-hash-level",
      SourceKind = LevelSourceKind.Local,
      LevelPath = legacyLevel.LevelPath,
      GameplayHash = currentHash,
      GameplayHashVersion = 3,
      FirstSeenAtUtc = "2026-01-01T00:01:00Z",
      LastSeenAtUtc = "2026-01-01T00:01:00Z",
    };
    string currentLevelId = LevelRepository.ResolveOrCreate(currentLevel, legacyHash, 2);

    Assert(currentLevelId != legacyLevelId, "Gameplay hash upgrade kept the obsolete v2 level identity.");
    Assert(
      LevelSessionRepository.Get("legacy-hash-session")?.LevelId == currentLevelId,
      "Gameplay hash upgrade did not repoint the legacy level session."
    );
    Assert(!LevelRepository.Exists(legacyLevelId), "Gameplay hash upgrade left an orphaned v2 level.");

    byte[] alternateLegacyHash = new byte[GameplayChartHash.Version2Size];
    alternateLegacyHash[0] = 4;
    legacyLevel.Id = "alternate-legacy-hash-level";
    legacyLevel.GameplayHash = alternateLegacyHash;
    string alternateLegacyLevelId = LevelRepository.ResolveOrCreate(legacyLevel);
    LevelSessionRepository.Save(
      new LevelSession
      {
        Id = "alternate-legacy-hash-session",
        LevelId = alternateLegacyLevelId,
        AppSessionId = "hash-upgrade-app",
        OpenedAtUtc = "2026-01-01T00:02:00Z",
      }
    );

    LevelRepository.MergeLegacyIdentity(currentLevelId, alternateLegacyHash, 2);
    Assert(
      LevelSessionRepository.Get("alternate-legacy-hash-session")?.LevelId == currentLevelId,
      "Same-session gameplay hash upgrade did not merge an alternate v2 identity."
    );
    Assert(
      !LevelRepository.Exists(alternateLegacyLevelId),
      "Same-session gameplay hash upgrade left an orphaned alternate v2 level."
    );
  }

  private static void TestGameplayHashV3Migration(string root)
  {
    string chartPath = Path.Combine(root, "gameplay-hash-v3-migration.adofai");
    File.WriteAllText(chartPath, "{}");
    byte[] currentHash = ComputeVersion3Hash(130f, "song.ogg", 100, 0, 100);
    byte[] version2Pitch100 = ComputeVersion2Hash(130f, "song.ogg", 100, 0, 100);
    byte[] version2Pitch150 = ComputeVersion2Hash(130f, "song.ogg", 150, 0, 100);
    byte[] version2Pitch70Quiet = ComputeVersion2Hash(130f, "song.ogg", 70, 0, 40);
    byte[] version1Hash;
    using (var writer = new GameplayChartHashCanonicalWriter())
    {
      writer.WriteAngles(new[] { 0f, 90f, 180f });
      version1Hash = writer.ComputeMd5Hash();
    }

    SetDatabasePath(Path.Combine(root, "gameplay-hash-v3-migration.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
      ActivitySchema.Ensure(connection);
    AppSessionRepository.Save(
      new AppSession
      {
        Id = "hash-v3-migration-app",
        StartedAtUtc = "2026-01-01T00:00:00Z",
        RecorderUtcOffsetMinutes = 0,
      }
    );

    string v1Session = SaveLegacyGameplayLevel(chartPath, version1Hash, 1, 100, "beta6");
    string v2Pitch100Session = SaveLegacyGameplayLevel(chartPath, version2Pitch100, 2, 100, "beta7-100");
    string v2Pitch150Session = SaveLegacyGameplayLevel(chartPath, version2Pitch150, 2, 150, "beta7-150");
    string v2QuietSession = SaveLegacyGameplayLevel(chartPath, version2Pitch70Quiet, 2, 70, "beta7-quiet");

    byte[] unmatchedHash = new byte[GameplayChartHash.Version2Size];
    unmatchedHash[0] = 0x7f;
    string unmatchedSession = SaveLegacyGameplayLevel(chartPath, unmatchedHash, 2, 120, "unmatched");
    string unmatchedLevelId = LevelSessionRepository.Get(unmatchedSession)?.LevelId;

    GameplayHashV3MigrationResult migrated = GameplayHashV3Migration.Run(
      (_, legacyHash, _, _) => GameplayChartHash.Equals(legacyHash, unmatchedHash) ? null : currentHash
    );
    Assert(migrated.Scanned == 5, "Gameplay hash migration did not scan every beta6/beta7 level.");
    Assert(migrated.Migrated == 4, "Gameplay hash migration did not merge every verified legacy level.");
    Assert(migrated.Deferred == 1, "Gameplay hash migration did not defer the unverifiable level.");

    string migratedLevelId = LevelSessionRepository.Get(v1Session)?.LevelId;
    Assert(!string.IsNullOrWhiteSpace(migratedLevelId), "Migrated beta6 level session disappeared.");
    Assert(
      LevelSessionRepository.Get(v2Pitch100Session)?.LevelId == migratedLevelId
        && LevelSessionRepository.Get(v2Pitch150Session)?.LevelId == migratedLevelId
        && LevelSessionRepository.Get(v2QuietSession)?.LevelId == migratedLevelId,
      "Verified beta6/beta7 pitch and hit-sound variants were not merged."
    );
    using (SqliteConnection connection = Database.OpenConnection())
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText = "SELECT gameplay_hash,gameplay_hash_version FROM levels WHERE id=@id";
      command.Parameters.AddWithValue("@id", migratedLevelId);
      using (SqliteDataReader reader = command.ExecuteReader())
      {
        Assert(reader.Read(), "Merged v3 level disappeared.");
        Assert(
          GameplayChartHash.Equals((byte[])reader.GetValue(0), currentHash)
            && reader.GetInt32(1) == GameplayChartHash.Version,
          "Merged legacy levels did not receive the v3 gameplay hash."
        );
      }

      command.Parameters["@id"].Value = unmatchedLevelId;
      using SqliteDataReader unmatchedReader = command.ExecuteReader();
      Assert(
        unmatchedReader.Read()
          && unmatchedReader.GetInt32(1) == 2
          && LevelSessionRepository.Get(unmatchedSession)?.LevelId == unmatchedLevelId,
        "Unverifiable beta7 data was modified instead of preserved."
      );
    }

    GameplayHashV3MigrationResult retried = GameplayHashV3Migration.Run((_, _, _, _) => currentHash);
    Assert(
      retried.Scanned == 1 && retried.Skipped == 1 && retried.Migrated == 0,
      "Unchanged deferred migration work was needlessly repeated."
    );

    using (SqliteConnection connection = Database.OpenConnection())
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText = "UPDATE gameplay_hash_migration_attempts SET result='unverified' WHERE level_id=@level";
      command.Parameters.AddWithValue("@level", unmatchedLevelId);
      command.ExecuteNonQuery();
    }
    GameplayHashV3MigrationResult recovered = GameplayHashV3Migration.Run((_, _, _, _) => currentHash);
    Assert(
      recovered.Scanned == 1 && recovered.Migrated == 1,
      "A beta migration row cached by the old startup timing was not retried."
    );
  }

  private static string SaveLegacyGameplayLevel(
    string chartPath,
    byte[] gameplayHash,
    int gameplayHashVersion,
    int pitch,
    string suffix
  )
  {
    string levelId = LevelRepository.ResolveOrCreate(
      new LevelRecord
      {
        SourceKind = LevelSourceKind.Local,
        LevelPath = chartPath,
        GameplayHash = gameplayHash,
        GameplayHashVersion = gameplayHashVersion,
        FirstSeenAtUtc = "2026-01-01T00:00:00Z",
        LastSeenAtUtc = "2026-01-01T00:00:00Z",
      }
    );
    string levelSessionId = "hash-v3-session-" + suffix;
    LevelSessionRepository.Save(
      new LevelSession
      {
        Id = levelSessionId,
        LevelId = levelId,
        AppSessionId = "hash-v3-migration-app",
        OpenedAtUtc = "2026-01-01T00:00:00Z",
      }
    );
    RunRepository.Save(
      new RunRecord
      {
        Id = "hash-v3-run-" + suffix,
        LevelSessionId = levelSessionId,
        StartedAtUtc = "2026-01-01T00:00:00Z",
        Result = "quit",
        LevelPitchPercent = pitch,
      }
    );
    return levelSessionId;
  }

  private static void TestBrokenRenamedForeignKeyRepair(string root)
  {
    string path = Path.Combine(root, "broken-renamed-foreign-keys.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      InsertRun(connection);

      using SqliteCommand breakSchema = connection.CreateCommand();
      breakSchema.CommandText =
        @"PRAGMA writable_schema=ON;
UPDATE sqlite_master
SET sql=replace(sql,'REFERENCES levels(id)','REFERENCES levels_new(id)')
WHERE type='table' AND name='level_sessions';
UPDATE sqlite_master
SET sql=replace(sql,'REFERENCES level_sessions(id)','REFERENCES level_sessions_new(id)')
WHERE type='table' AND name='runs';
PRAGMA writable_schema=OFF;
PRAGMA user_version=13;";
      breakSchema.ExecuteNonQuery();
    }

    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);

      using SqliteCommand verify = connection.CreateCommand();
      verify.CommandText = "PRAGMA user_version;";
      Assert(Convert.ToInt32(verify.ExecuteScalar()) == ActivitySchema.Version, "Broken schema was not upgraded.");

      verify.CommandText = "SELECT \"table\" FROM pragma_foreign_key_list('level_sessions') WHERE \"from\"='level_id';";
      Assert(Convert.ToString(verify.ExecuteScalar()) == "levels", "Level-session foreign key was not repaired.");

      verify.CommandText = "SELECT \"table\" FROM pragma_foreign_key_list('runs') WHERE \"from\"='level_session_id';";
      Assert(Convert.ToString(verify.ExecuteScalar()) == "level_sessions", "Run foreign key was not repaired.");

      verify.CommandText = "SELECT count(*) FROM levels WHERE id='logical';";
      Assert(Convert.ToInt32(verify.ExecuteScalar()) == 1, "Foreign-key repair lost the level.");
      verify.CommandText = "SELECT count(*) FROM level_sessions WHERE id='level';";
      Assert(Convert.ToInt32(verify.ExecuteScalar()) == 1, "Foreign-key repair lost the level session.");
      verify.CommandText = "SELECT count(*) FROM runs WHERE id='run';";
      Assert(Convert.ToInt32(verify.ExecuteScalar()) == 1, "Foreign-key repair lost the run.");

      verify.CommandText = "PRAGMA foreign_key_check;";
      using SqliteDataReader violations = verify.ExecuteReader();
      Assert(!violations.Read(), "Foreign-key repair left a violation.");
    }
  }

  private static byte[] ComputeVersion2Hash(
    float bpm,
    string songFilename,
    int pitch,
    byte hitsound,
    int hitsoundVolume
  )
  {
    using var writer = new GameplayChartHashCanonicalWriter();
    writer.WriteFormatVersion(2);
    writer.WriteGameplaySettingsV2(15, songFilename, bpm, 100, 0, pitch, hitsound, hitsoundVolume, false, 4, 0f, false);
    writer.WriteChartKind(false);
    writer.WriteAngles(new[] { 0f, 90f, 180f });
    return writer.ComputeSha256Hash();
  }

  private static byte[] ComputeVersion3Hash(
    float bpm,
    string songFilename,
    int pitch,
    byte hitsound,
    int hitsoundVolume
  )
  {
    _ = pitch;
    _ = hitsound;
    _ = hitsoundVolume;
    using var writer = new GameplayChartHashCanonicalWriter();
    writer.WriteFormatVersion(3);
    writer.WriteGameplaySettingsV3(15, songFilename, bpm, 100, 0, false, 4, 0f, false);
    writer.WriteChartKind(false);
    writer.WriteAngles(new[] { 0f, 90f, 180f });
    return writer.ComputeSha256Hash();
  }

  private static void TestLegacyLevelMigration(string root, int version)
  {
    string path = Path.Combine(root, "level-migration-v" + version + ".sqlite");
    using var connection = new SqliteConnection("Data Source=" + path);
    connection.Open();
    using (SqliteCommand setup = connection.CreateCommand())
    {
      setup.CommandText =
        @"CREATE TABLE app_sessions(
  id TEXT PRIMARY KEY,started_at_utc TEXT NOT NULL,ended_at_utc TEXT,
  recorder_time_zone_id TEXT,recorder_utc_offset_minutes INTEGER NOT NULL
);
CREATE TABLE logical_levels(
  id TEXT PRIMARY KEY,identity_key TEXT NOT NULL UNIQUE,tuf_level_id INTEGER,gameplay_hash BLOB,
  gameplay_hash_version INTEGER,song TEXT,author TEXT,artist TEXT,
  first_seen_at_utc TEXT NOT NULL,last_seen_at_utc TEXT NOT NULL
);
CREATE TABLE level_sessions(
  id TEXT PRIMARY KEY,logical_level_id TEXT NOT NULL,app_session_id TEXT NOT NULL,tuf_level_id INTEGER,
  level_path TEXT NOT NULL,opened_at_utc TEXT NOT NULL,closed_at_utc TEXT,
  level_tile_count INTEGER NOT NULL DEFAULT 0,level_file_hash BLOB,gameplay_hash BLOB,
  gameplay_hash_version INTEGER,song TEXT,author TEXT,artist TEXT,metadata_state INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE runs(
  id TEXT PRIMARY KEY,level_session_id TEXT NOT NULL,run_index INTEGER NOT NULL,started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,start_tile INTEGER NOT NULL DEFAULT 0,last_tile INTEGER,result TEXT NOT NULL DEFAULT 'unknown',
  no_fail_mode INTEGER NOT NULL DEFAULT 0,gameplay_start_song_position REAL,level_pitch_percent INTEGER,
  effective_pitch REAL,x_accuracy REAL,judgment_difficulty INTEGER,judgment_overload INTEGER NOT NULL DEFAULT 0,
  judgment_too_early INTEGER NOT NULL DEFAULT 0,judgment_early INTEGER NOT NULL DEFAULT 0,
  judgment_early_perfect INTEGER NOT NULL DEFAULT 0,judgment_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late_perfect INTEGER NOT NULL DEFAULT 0,judgment_late INTEGER NOT NULL DEFAULT 0,
  judgment_too_late INTEGER NOT NULL DEFAULT 0,judgment_miss INTEGER NOT NULL DEFAULT 0,
  gameplay_hash BLOB,gameplay_hash_version INTEGER,input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',meta_json TEXT NOT NULL DEFAULT '{}'
);
CREATE TABLE microphone_recordings(
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,audio_wav BLOB NOT NULL,format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,channels INTEGER NOT NULL,frame_count INTEGER NOT NULL,device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0,is_permanent INTEGER NOT NULL DEFAULT 0,expires_at_utc TEXT
);
CREATE INDEX idx_level_sessions_logical ON level_sessions(logical_level_id,opened_at_utc,id);
CREATE INDEX idx_level_sessions_app ON level_sessions(app_session_id,opened_at_utc,id);
CREATE INDEX idx_runs_level_index ON runs(level_session_id,run_index);
CREATE INDEX idx_runs_start_tile ON runs(level_session_id,start_tile,run_index);
INSERT INTO app_sessions VALUES('legacy-app','2026-01-01',NULL,NULL,0);
INSERT INTO logical_levels VALUES('legacy-logical','legacy',NULL,X'01020304',1,NULL,NULL,NULL,'2026-01-01','2026-01-01');
INSERT INTO level_sessions VALUES(
  'legacy-session','legacy-logical','legacy-app',NULL,'legacy.adofai','2026-01-01',NULL,12,NULL,
  X'0102030405060708090A0B0C0D0E0F10',1,'Song','Author','Artist',1
);
INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,start_tile,result,gameplay_hash,gameplay_hash_version
) VALUES('legacy-run','legacy-session',0,'2026-01-01',0,'cleared',X'0102030405060708090A0B0C0D0E0F10',1);";
      setup.ExecuteNonQuery();
      if (version == 12)
      {
        setup.CommandText =
          @"CREATE TABLE gameplay_snapshots(
  gameplay_hash BLOB NOT NULL,gameplay_hash_version INTEGER NOT NULL,chart_json_utf8 BLOB NOT NULL,
  created_at_utc TEXT NOT NULL,PRIMARY KEY(gameplay_hash,gameplay_hash_version)
);";
        setup.ExecuteNonQuery();
      }
      setup.CommandText = "PRAGMA user_version=" + version;
      setup.ExecuteNonQuery();
    }

    ActivitySchema.Ensure(connection);
    using SqliteCommand verify = connection.CreateCommand();
    verify.CommandText =
      "SELECT s.level_id,l.adofai_path,l.gameplay_hash_version FROM level_sessions s JOIN levels l ON l.id=s.level_id";
    using SqliteDataReader reader = verify.ExecuteReader();
    Assert(reader.Read(), "Legacy level session was not migrated from v" + version + ".");
    Assert(reader.GetString(1) == "legacy.adofai" && reader.GetInt32(2) == 1, "Legacy level data changed.");
    reader.Close();
    verify.CommandText =
      "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='gameplay_hash_migration_attempts'";
    Assert(Convert.ToInt32(verify.ExecuteScalar()) == 1, "Gameplay hash migration state table is missing.");
    connection.Close();

    SetDatabasePath(path);
    AppSessionRepository.Save(
      new AppSession
      {
        Id = "post-migration-app-" + version,
        StartedAtUtc = "2026-01-02",
        RecorderUtcOffsetMinutes = 0,
      }
    );
    Assert(
      CountRows("app_sessions", "id='post-migration-app-" + version + "'") == 1,
      "First app-session write failed after migrating v" + version + "."
    );
  }

  private static void TestRunDeletionHierarchy(string root)
  {
    SetDatabasePath(Path.Combine(root, "run-deletion.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand seed = connection.CreateCommand();
      seed.CommandText =
        @"
INSERT INTO app_sessions(id,started_at_utc,ended_at_utc,recorder_utc_offset_minutes)
VALUES('closed-app','2026-01-01','2026-01-02',0),('open-app','2026-01-03',NULL,0);
INSERT INTO levels(
  id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc
) VALUES('closed-logical','closed',0,'closed.adofai','2026-01-01','2026-01-02'),
        ('open-logical','open',0,'open.adofai','2026-01-03','2026-01-03');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
VALUES('closed-level','closed-logical','closed-app','2026-01-01','2026-01-02'),
      ('open-level','open-logical','open-app','2026-01-03',NULL);
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result)
VALUES('closed-run-1','closed-level',0,'2026-01-01',0,'clear'),
      ('closed-run-2','closed-level',1,'2026-01-01',0,'clear'),
      ('open-run','open-level',0,'2026-01-03',0,'clear');
INSERT INTO microphone_recordings(
  run_id,audio_wav,format,sample_rate,channels,frame_count,capture_start_offset_us,is_permanent
) VALUES('closed-run-1',X'00','wav/pcm16',48000,1,1,0,1);";
      seed.ExecuteNonQuery();
    }

    Assert(!RunRepository.Delete("missing-run"), "Missing run deletion succeeded.");
    Assert(RunRepository.Delete("closed-run-1"), "Existing run was not deleted.");
    Assert(!RunRepository.Exists("closed-run-1"), "Deleted run remained in the database.");
    Assert(RunRepository.Exists("closed-run-2"), "Sibling run was deleted.");
    Assert(CountRows("microphone_recordings") == 0, "Run deletion did not cascade to its microphone recording.");
    Assert(CountRows("level_sessions", "id='closed-level'") == 1, "Non-empty level session was pruned.");

    Assert(RunRepository.Delete("closed-run-2"), "Last closed-session run was not deleted.");
    Assert(CountRows("level_sessions", "id='closed-level'") == 0, "Empty closed level session remained.");
    Assert(CountRows("levels", "id='closed-logical'") == 0, "Orphan level remained.");
    Assert(CountRows("app_sessions", "id='closed-app'") == 0, "Empty closed app session remained.");

    Assert(RunRepository.Delete("open-run"), "Open-session run was not deleted.");
    Assert(CountRows("level_sessions", "id='open-level'") == 1, "Open level session was pruned.");
    Assert(CountRows("levels", "id='open-logical'") == 1, "Open level was pruned.");
    Assert(CountRows("app_sessions", "id='open-app'") == 1, "Open app session was pruned.");
  }

  private static int CountRows(string table, string where = "1=1")
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM " + table + " WHERE " + where;
    return Convert.ToInt32(command.ExecuteScalar());
  }

  private static void TestLogicalRunSessionFilter(string root)
  {
    SetDatabasePath(Path.Combine(root, "logical-run-filter.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand seed = connection.CreateCommand();
      seed.CommandText =
        @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes)
VALUES('day-a','2026-01-01',0),('day-b','2026-01-02',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc)
VALUES('shared-logical','shared',0,'shared.adofai','2026-01-01','2026-01-02');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc)
VALUES('level-a','shared-logical','day-a','2026-01-01'),
      ('level-b','shared-logical','day-b','2026-01-02');
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result)
VALUES('run-a','level-a',0,'2026-01-01',0,'clear'),
      ('run-b','level-b',0,'2026-01-02',0,'clear');";
      seed.ExecuteNonQuery();
    }

    List<RunRecord> firstDay = RunRepository.ListByLogicalLevel("shared-logical", new List<string> { "day-a" }, 0, 200);
    Assert(firstDay.Count == 1 && firstDay[0].Id == "run-a", "Logical run query crossed day sessions.");
  }

  private static void TestLogicalLevelIdentity(string root)
  {
    SetDatabasePath(Path.Combine(root, "logical-levels.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
      ActivitySchema.Ensure(connection);
    AppSessionRepository.Save(
      new AppSession
      {
        Id = "logical-app",
        StartedAtUtc = "2026-01-01T00:00:00Z",
        RecorderUtcOffsetMinutes = 0,
      }
    );

    LevelSession gameplayA = LogicalVisit("gameplay-a", "/tmp/a.adofai", null, new byte[32]);
    LevelSession gameplayB = LogicalVisit("gameplay-b", "/tmp/b.adofai", null, new byte[32]);
    LevelSessionRepository.Save(gameplayA);
    LevelSessionRepository.Save(gameplayB);
    Assert(gameplayA.LevelId != gameplayB.LevelId, "Different local paths were incorrectly merged.");

    LevelSession tufA = LogicalVisit("tuf-a", "/tmp/tuf-a.adofai", 77, new byte[32]);
    LevelSession tufB = LogicalVisit("tuf-b", "/tmp/tuf-b.adofai", 77, Enumerable.Repeat((byte)9, 32).ToArray());
    LevelSessionRepository.Save(tufA);
    LevelSessionRepository.Save(tufB);
    Assert(tufA.LevelId != tufB.LevelId, "Different TUF gameplay revisions were incorrectly merged.");

    LevelSession fileA = LogicalVisit("file-a", "/tmp/local.adofai", null, new byte[32]);
    LevelSession fileB = LogicalVisit("file-b", "/tmp/local.adofai", null, new byte[32]);
    LevelSession fileChanged = LogicalVisit(
      "file-changed",
      "/tmp/local.adofai",
      null,
      Enumerable.Repeat((byte)6, 32).ToArray()
    );
    LevelSessionRepository.Save(fileA);
    LevelSessionRepository.Save(fileB);
    LevelSessionRepository.Save(fileChanged);
    Assert(fileA.LevelId == fileB.LevelId, "Equal local gameplay revisions were not merged.");
    Assert(fileA.LevelId != fileChanged.LevelId, "Changed local gameplay revisions were incorrectly merged.");
  }

  private static LevelSession LogicalVisit(string id, string path, int? tufLevelId, byte[] gameplayHash)
  {
    string timestamp = "2026-01-01T00:00:00Z";
    string levelId = LevelRepository.ResolveOrCreate(
      new LevelRecord
      {
        Id = id,
        SourceKind = tufLevelId.HasValue ? LevelSourceKind.Tuf : LevelSourceKind.Local,
        TufLevelId = tufLevelId,
        LevelPath = path,
        LevelTileCount = 100,
        GameplayHash = gameplayHash,
        GameplayHashVersion = GameplayChartHash.Version,
        MetadataState = LevelMetadataState.Unavailable,
        FirstSeenAtUtc = timestamp,
        LastSeenAtUtc = timestamp,
      }
    );
    return new LevelSession
    {
      Id = id,
      LevelId = levelId,
      AppSessionId = "logical-app",
      OpenedAtUtc = timestamp,
    };
  }

  private static void InsertRun(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes) VALUES('app','2026-01-01',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc) VALUES('logical','test',0,'test.adofai','2026-01-01','2026-01-01');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc) VALUES('level','logical','app','2026-01-01');
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result,judgment_difficulty,no_fail_mode) VALUES('run','level',0,'2026-01-01',0,'cleared',1,1);";
    command.ExecuteNonQuery();
  }

  private static void SetDatabasePath(string path)
  {
    PropertyInfo property = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    property.SetValue(null, path);
    MicrophoneDatabase.Initialize(
      Path.Combine(
        Path.GetDirectoryName(path) ?? "",
        Path.GetFileNameWithoutExtension(path) + ".microphones.sqlite"
      )
    );
  }

  private static void Assert(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }

  private static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
  {
    try
    {
      action();
    }
    catch (TException)
    {
      return;
    }
    throw new InvalidOperationException(message);
  }

  private static StoredMicrophoneRecording PlaybackRecording(
    string path,
    long frameCount,
    int sampleRate = 48000,
    int channels = 1
  )
  {
    return new StoredMicrophoneRecording
    {
      RunId = "test-run",
      FilePath = path,
      Format = "wav/pcm16",
      SampleRate = sampleRate,
      Channels = channels,
      FrameCount = frameCount,
      CaptureStartOffsetUs = 0L,
      ByteLength = new FileInfo(path).Length,
    };
  }

  private static void WriteTestPcm16Wave(string path, int sampleRate, short channels, short[] samples)
  {
    using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    using var writer = new BinaryWriter(stream);
    int dataLength = samples.Length * 2;
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + dataLength);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
    writer.Write(16);
    writer.Write((short)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * 2);
    writer.Write((short)(channels * 2));
    writer.Write((short)16);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
    writer.Write(dataLength);
    foreach (short sample in samples)
      writer.Write(sample);
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
