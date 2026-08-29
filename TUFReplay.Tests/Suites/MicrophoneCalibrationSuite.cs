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

internal static class MicrophoneCalibrationSuite
{
  internal static void RunAll(string root)
  {
    TestMicrophoneCaptureChunking();
    TestPendingMicrophoneDisposition();
    TestHitMarginSnapshotReuse();
    TestWavWriter(root);
    TestPlaybackWaveReader(root);
    TestPcm16PrefetchBuffer();
    TestPlaybackLimiter(root);
    TestMicrophoneTimelineAnchor();
    TestReplayMicrophoneClock();
    TestCalibrationSettings(root);
    TestMicrophoneCalibrationState();
    TestCalibrationWaveforms(root);
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
    var disposition = new PendingMicrophoneDisposition(
      (value, shouldPersist) =>
      {
        completed++;
        observed = value;
        persisted = shouldPersist;
      }
    );

    disposition.CompleteDisposition(persist: true);
    Assert(completed == 0, "Microphone disposition ran before capture finalization.");
    disposition.CompleteCapture(recording);
    Assert(completed == 1, "Microphone disposition did not run after both gates completed.");
    Assert(ReferenceEquals(observed, recording) && persisted, "Microphone persistence decision was lost.");
    disposition.CompleteDisposition(persist: false);
    disposition.CompleteCapture(recording);
    Assert(completed == 1, "Microphone disposition completed more than once.");

    completed = 0;
    var captureFirst = new PendingMicrophoneDisposition(
      (_, shouldPersist) =>
      {
        completed++;
        persisted = shouldPersist;
      }
    );
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
    Assert(
      snapshot.TryGetSingleIncrement(new[] { 4, 6, 6 }, out int incrementedMargin) && incrementedMargin == 1,
      "A single resolved hit margin increment was not detected."
    );
    Assert(
      !snapshot.TryGetSingleIncrement(new[] { 5, 6, 6 }, out _),
      "Multiple hit margin increments were accepted as one judgment."
    );

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

  private static void TestPcm16PrefetchBuffer()
  {
    byte[] source = new byte[24];
    for (int i = 0; i < source.Length; i++)
      source[i] = (byte)i;

    int callbackThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
    using var stream = new ThreadTrackingMemoryStream(source);
    using var prefetch = new Pcm16PrefetchBuffer(stream, 4, 16, 8);

    byte[] first = new byte[6];
    Assert(ReadPrefetched(prefetch, first, 6) == 6, "PCM prefetch did not fill the initial read.");
    Assert(first[0] == 4 && first[5] == 9, "PCM prefetch did not honor the WAV data offset or preserve byte order.");

    prefetch.Seek(8);
    byte[] sought = new byte[6];
    Assert(ReadPrefetched(prefetch, sought, 6) == 6, "PCM prefetch did not refill after seek.");
    Assert(sought[0] == 12 && sought[5] == 17, "PCM prefetch seek returned stale buffered data.");

    prefetch.Seek(14);
    byte[] tail = new byte[4];
    Assert(ReadPrefetched(prefetch, tail, 2, 4) == 2, "PCM prefetch did not stop at the data boundary.");
    Assert(tail[0] == 18 && tail[1] == 19, "PCM prefetch returned the wrong tail bytes.");
    Assert(stream.ReadThreadId != callbackThreadId, "PCM bytes were read on the consumer thread.");
    Assert(prefetch.Failure == null, "PCM prefetch worker failed during normal reads.");
  }

  private static int ReadPrefetched(Pcm16PrefetchBuffer prefetch, byte[] destination, int expected, int count = -1)
  {
    int requested = count < 0 ? destination.Length : count;
    int total = 0;
    for (int attempt = 0; attempt < 100 && total < expected; attempt++)
    {
      int read = prefetch.Read(destination, total, requested - total, 2);
      total += read;
      if (read == 0)
        System.Threading.Thread.Sleep(1);
    }

    return total;
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
    float gainAtThirtyDb = MicrophoneGain.FromDecibels(30);
    float boostedQuietGain = envelope.RequiredLimiterGain(0, gainAtThirtyDb) * gainAtThirtyDb;
    Assert(boostedQuietGain > 10f, "+30 dB did not amplify quiet audio beyond the old +20 dB maximum.");
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
    float limitedGainAtThirtyDb = envelope.RequiredLimiterGain(transientFrame, gainAtThirtyDb) * gainAtThirtyDb;
    Assert(
      limitedGainAtThirtyDb <= Pcm16LimiterEnvelope.Ceiling + 0.0001f,
      "Limiter exceeded its true-peak ceiling at +30 dB."
    );
    Assert(Pcm16LimiterEnvelope.Ceiling > 0.96f, "Limiter ceiling was not relaxed to -0.3 dBFS.");

    var limiter = new Pcm16Limiter(envelope, sampleRate);
    float peakGain = 10f;
    for (int frame = transientFrame - sampleRate * 7 / 1000; frame <= transientFrame; frame++)
      peakGain = limiter.NextEffectiveGain(frame, 10f);
    float releaseGain = peakGain;
    for (int frame = transientFrame + 1; frame <= transientFrame + sampleRate * 30 / 1000; frame++)
      releaseGain = limiter.NextEffectiveGain(frame, 10f);
    Assert(releaseGain > 5f && releaseGain < 10f, "Limiter did not recover quickly with the relaxed release.");

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

  private static void TestMicrophoneTimelineAnchor()
  {
    Assert(
      MicrophoneTimelineAnchor.CalculateCorrectionUs(0L, 1d, 3.2d) == -3_200_000L,
      "Normal microphone preroll correction is incorrect."
    );
    Assert(
      MicrophoneTimelineAnchor.CalculateCorrectionUs(500_000L, 1d, 3d) == -2_500_000L,
      "Frozen countdown correction did not preserve the resumed timeline position."
    );
    Assert(
      MicrophoneTimelineAnchor.CalculateCorrectionUs(750_000L, 1.5d, 3d) == -2_500_000L,
      "Frozen countdown correction did not account for gameplay pitch."
    );
    Assert(
      MicrophoneTimelineAnchor.CalculateCorrectionUs(500_000L, double.NaN, 1d) == -500_000L,
      "Invalid gameplay pitch did not fall back to real-time playback."
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
    Assert(Math.Abs(MicrophoneGain.FromDecibels(30) - 31.622776f) < 0.0001f, "+30 dB gain is incorrect.");

    string timingPath = Path.Combine(root, "timing-settings.json");
    TUFReplaySettingStore.Initialize(timingPath);
    MicrophoneTimingSettingsState timing = MicrophoneTimingSettingsService.SetOffset(999);
    timing = MicrophoneTimingSettingsService.SetVolume(-50);
    Assert(timing.MicrophoneOffsetMs == TUFReplaySetting.MaxMicrophoneOffsetMs, "Timing offset was not clamped.");
    Assert(timing.MicrophoneVolumeDb == TUFReplaySetting.MinMicrophoneVolumeDb, "Timing gain was not clamped.");
    timing = MicrophoneTimingSettingsService.SetVolume(100);
    Assert(timing.MicrophoneVolumeDb == 30, "Timing gain was not clamped to +30 dB.");
    TUFReplaySetting persistedTiming = TUFReplaySetting.Load(timingPath);
    Assert(persistedTiming.MicrophoneOffsetMs == timing.MicrophoneOffsetMs, "Timing offset was not persisted.");
    Assert(persistedTiming.MicrophoneVolumeDb == timing.MicrophoneVolumeDb, "Timing gain was not persisted.");
  }

  private static void TestMicrophoneCalibrationState()
  {
    var state = new MicrophoneCalibrationState();
    state.BeginMeasurement("first", 3);
    Assert(state.GetStatus().MicrophoneOffsetMs == 0, "The first calibration did not start from raw timing.");
    state.SetOffset(96);
    state.SetVolume(30);
    Assert(state.GetStatus().MicrophoneVolumeDb == 30, "Calibration did not accept +30 dB preview gain.");

    state.BeginMeasurement("second", 3);
    MicrophoneCalibrationStatus second = state.GetStatus();
    Assert(second.OperationId == "second", "The second calibration operation was not started.");
    Assert(second.MicrophoneOffsetMs == 0, "The second calibration reused the previous correction.");
    Assert(second.MicrophoneVolumeDb == 3, "The calibration microphone volume was not preserved.");
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

  private sealed class ThreadTrackingMemoryStream : MemoryStream
  {
    private int _readThreadId;

    public ThreadTrackingMemoryStream(byte[] buffer)
      : base(buffer) { }

    public int ReadThreadId => System.Threading.Volatile.Read(ref _readThreadId);

    public override int Read(byte[] buffer, int offset, int count)
    {
      System.Threading.Volatile.Write(ref _readThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId);
      return base.Read(buffer, offset, count);
    }
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
}
