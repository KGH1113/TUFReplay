using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay;
using TUFReplay.Application.Calibration;
using TUFReplay.Application.Microphone;
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
      TestWavWriter(root);
      TestPlaybackWaveReader(root);
      TestReplayMicrophoneClock();
      TestCalibrationSettings(root);
      TestCalibrationWaveforms(root);
      TestAdofaiLevelFileHash(root);
      TestSchemaMigrationAndBlob(root);
      TestLogicalLevelIdentity(root);
      TestReplayInputStableOrder();
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
    List<RecordedInput> inputs = ReplayInputParser.Parse(csv);

    Assert(inputs.Count == 4, "Replay input parser dropped valid events.");
    Assert(inputs[0].Key == 7, "Replay input parser did not sort timestamps.");
    Assert(inputs[1].Key == 9 && inputs[2].Key == 8 && inputs[3].Key == 9, "Same-time input order changed.");
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
  }

  private static RecordedInput Input(long timeUs, int key, bool down)
  {
    RecordInputFlags flags = RecordInputFlags.Async;
    if (down)
      flags |= RecordInputFlags.Down;
    return new RecordedInput(timeUs, key, flags);
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

  private static void TestReplayMicrophoneClock()
  {
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
      game[CalibrationWaveformBuilder.BinCount / 2] > 0.99f
        && game[CalibrationWaveformBuilder.BinCount * 4 / 5] == 0f,
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
    Assert(
      referenceSong[CalibrationWaveformBuilder.BinCount / 3] > 0.99f,
      "Reference song waveform timing is wrong."
    );
    Assert(
      CalibrationWaveformBuilder.ReferenceStartFrame(1d, 0.2d, 0.1d, 2d, 1000, false) == 1400,
      "Modern conductor input offset was not applied to the song reference."
    );
    Assert(
      CalibrationWaveformBuilder.ReferenceStartFrame(1d, 0.2d, -0.1d, 2d, 1000, false) == 1000,
      "Negative input offset moved the song reference in the wrong direction."
    );
    Assert(
      CalibrationWaveformBuilder.ReferenceStartFrame(1d, 0.2d, 0.1d, 2d, 1000, true) == 1200,
      "Legacy conductor input offset was not applied to the song reference."
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
      Assert(Convert.ToInt32(version.ExecuteScalar()) == 11, "Fresh schema version is not 11.");
      InsertRun(connection);
    }

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

    using (SqliteConnection connection = Database.OpenConnection())
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
      reader.Close();

      using SqliteCommand delete = connection.CreateCommand();
      delete.CommandText = "DELETE FROM runs WHERE id='run'";
      delete.ExecuteNonQuery();
      using SqliteCommand cascade = connection.CreateCommand();
      cascade.CommandText = "SELECT count(*) FROM microphone_recordings";
      Assert(Convert.ToInt32(cascade.ExecuteScalar()) == 0, "Run deletion did not cascade to microphone recording.");
    }

    string migrationPath = Path.Combine(root, "migration.sqlite");
    File.Copy(path, migrationPath);
    using var migration = new SqliteConnection("Data Source=" + migrationPath);
    migration.Open();
    using (SqliteCommand setup = migration.CreateCommand())
    {
      setup.CommandText =
        "DROP INDEX idx_level_sessions_logical; ALTER TABLE level_sessions DROP COLUMN logical_level_id; ALTER TABLE level_sessions DROP COLUMN gameplay_hash; ALTER TABLE level_sessions DROP COLUMN gameplay_hash_version; DROP TABLE logical_levels; ALTER TABLE level_sessions DROP COLUMN level_file_hash; DROP TABLE microphone_recordings; PRAGMA user_version=7; PRAGMA foreign_keys=ON;";
      setup.ExecuteNonQuery();
    }
    ActivitySchema.Ensure(migration);
    using SqliteCommand migrated = migration.CreateCommand();
    migrated.CommandText = "SELECT user_version FROM pragma_user_version";
    Assert(Convert.ToInt32(migrated.ExecuteScalar()) == 11, "Schema 7 to 11 migration failed.");

    string retentionMigrationPath = Path.Combine(root, "retention-migration.sqlite");
    using var retentionMigration = new SqliteConnection("Data Source=" + retentionMigrationPath);
    retentionMigration.Open();
    using (SqliteCommand setup = retentionMigration.CreateCommand())
    {
      setup.CommandText =
        @"
CREATE TABLE app_sessions(id TEXT PRIMARY KEY,started_at_utc TEXT NOT NULL);
CREATE TABLE level_sessions(
  id TEXT PRIMARY KEY,app_session_id TEXT NOT NULL,tuf_level_id INTEGER,level_path TEXT NOT NULL,
  opened_at_utc TEXT NOT NULL,closed_at_utc TEXT,level_tile_count INTEGER NOT NULL DEFAULT 0,
  level_file_hash BLOB,song TEXT,author TEXT,artist TEXT,metadata_state INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE runs(
  id TEXT PRIMARY KEY,level_session_id TEXT NOT NULL,run_index INTEGER NOT NULL,start_tile INTEGER NOT NULL,
  gameplay_hash BLOB,gameplay_hash_version INTEGER
);
CREATE TABLE microphone_recordings(
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,
  audio_wav BLOB NOT NULL,format TEXT NOT NULL,sample_rate INTEGER NOT NULL,
  channels INTEGER NOT NULL,frame_count INTEGER NOT NULL,device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0
);
INSERT INTO app_sessions(id,started_at_utc) VALUES('legacy-app','2026-01-01T00:00:00Z');
INSERT INTO level_sessions(id,app_session_id,level_path,opened_at_utc)
VALUES('legacy-level','legacy-app','legacy.adofai','2026-01-01T00:00:00Z');
INSERT INTO runs(id,level_session_id,run_index,start_tile) VALUES('legacy-run','legacy-level',0,0);
INSERT INTO microphone_recordings(run_id,audio_wav,format,sample_rate,channels,frame_count)
VALUES('legacy-run',X'00','wav/pcm16',48000,1,1);
PRAGMA user_version=9;";
      setup.ExecuteNonQuery();
    }
    ActivitySchema.Ensure(retentionMigration);
    using SqliteCommand retention = retentionMigration.CreateCommand();
    retention.CommandText =
      "SELECT is_permanent,expires_at_utc FROM microphone_recordings WHERE run_id='legacy-run'";
    using SqliteDataReader retentionReader = retention.ExecuteReader();
    Assert(retentionReader.Read(), "Legacy microphone recording was lost during retention migration.");
    Assert(
      retentionReader.GetInt32(0) == 1 && retentionReader.IsDBNull(1),
      "Legacy microphone recording was not migrated as permanent."
    );
  }

  private static void TestAdofaiLevelFileHash(string root)
  {
    string path = Path.Combine(root, "level-hash.adofai");
    File.WriteAllText(path, "{\"angleData\":[0,90]}");
    Assert(AdofaiLevelFileHash.TryCompute(path, out byte[] first), "Level file hash was not computed.");
    Assert(first.Length == AdofaiLevelFileHash.Size, "Level file hash length is invalid.");
    Assert(AdofaiLevelFileHash.TryCompute(path, out byte[] same), "Repeated level file hash failed.");
    Assert(AdofaiLevelFileHash.Equals(first, same), "Unchanged level file hash changed.");

    File.WriteAllText(path, "{\"angleData\":[0,180]}");
    Assert(AdofaiLevelFileHash.TryCompute(path, out byte[] changed), "Changed level file hash failed.");
    Assert(!AdofaiLevelFileHash.Equals(first, changed), "Changed level file was treated as identical.");
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

    LevelSession gameplayA = LogicalVisit("gameplay-a", "/tmp/a.adofai", null, new byte[16], new byte[] { 1 });
    LevelSession gameplayB = LogicalVisit("gameplay-b", "/tmp/b.adofai", null, new byte[16], new byte[] { 2 });
    LevelSessionRepository.Save(gameplayA);
    LevelSessionRepository.Save(gameplayB);
    Assert(gameplayA.LogicalLevelId == gameplayB.LogicalLevelId, "Equal gameplay hashes were not merged.");

    LevelSession tufA = LogicalVisit("tuf-a", "/tmp/tuf-a.adofai", 77, new byte[16], new byte[] { 3 });
    LevelSession tufB = LogicalVisit("tuf-b", "/tmp/tuf-b.adofai", 77, Enumerable.Repeat((byte)9, 16).ToArray(), new byte[] { 4 });
    LevelSessionRepository.Save(tufA);
    LevelSessionRepository.Save(tufB);
    Assert(tufA.LogicalLevelId == tufB.LogicalLevelId, "Equal TUF IDs were not merged after chart updates.");

    LevelSession fileA = LogicalVisit("file-a", "/tmp/local.adofai", null, null, new byte[] { 5 });
    LevelSession fileB = LogicalVisit("file-b", "/tmp/local.adofai", null, null, new byte[] { 5 });
    LevelSession fileChanged = LogicalVisit("file-changed", "/tmp/local.adofai", null, null, new byte[] { 6 });
    LevelSessionRepository.Save(fileA);
    LevelSessionRepository.Save(fileB);
    LevelSessionRepository.Save(fileChanged);
    Assert(fileA.LogicalLevelId == fileB.LogicalLevelId, "Equal local file identities were not merged.");
    Assert(fileA.LogicalLevelId != fileChanged.LogicalLevelId, "Changed local files were incorrectly merged.");
  }

  private static LevelSession LogicalVisit(
    string id,
    string path,
    int? tufLevelId,
    byte[] gameplayHash,
    byte[] fileHash
  ) =>
    new LevelSession
    {
      Id = id,
      AppSessionId = "logical-app",
      TufLevelId = tufLevelId,
      LevelPath = path,
      OpenedAtUtc = "2026-01-01T00:00:00Z",
      LevelTileCount = 100,
      GameplayHash = gameplayHash,
      GameplayHashVersion = gameplayHash == null ? null : (int?)GameplayChartHash.Version,
      LevelFileHash = fileHash,
      MetadataState = LevelMetadataState.Unavailable,
    };

  private static void InsertRun(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes) VALUES('app','2026-01-01',0);
INSERT INTO logical_levels(id,identity_key,first_seen_at_utc,last_seen_at_utc) VALUES('logical','test','2026-01-01','2026-01-01');
INSERT INTO level_sessions(id,logical_level_id,app_session_id,level_path,opened_at_utc) VALUES('level','logical','app','test.adofai','2026-01-01');
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,result) VALUES('run','level',0,'2026-01-01',0,'cleared');";
    command.ExecuteNonQuery();
  }

  private static void SetDatabasePath(string path)
  {
    PropertyInfo property = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    property.SetValue(null, path);
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

  private static StoredMicrophoneRecording PlaybackRecording(string path, long frameCount)
  {
    return new StoredMicrophoneRecording
    {
      RunId = "test-run",
      FilePath = path,
      Format = "wav/pcm16",
      SampleRate = 48000,
      Channels = 1,
      FrameCount = frameCount,
      CaptureStartOffsetUs = 0L,
      ByteLength = new FileInfo(path).Length,
    };
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
