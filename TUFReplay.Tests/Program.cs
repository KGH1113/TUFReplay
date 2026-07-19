using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay;
using TUFReplay.Application.Calibration;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Replay;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Database.Schema;
using TUFReplay.Infrastructure.NativeInput;

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
      TestSchemaMigrationAndBlob(root);
      TestReplayInputStableOrder();
      TestReplaySchedulerChord();
      TestReplayPumpTimingAndBatching();
      TestReplayPumpFocusAndReleaseAll();
      TestReplayPumpClockJumpSeeksState();
      TestPreparedReplayDoesNotEmit();
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
    Assert(legacy.MicrophoneOffsetMs == 0, "Legacy calibration offset default is incorrect.");
    Assert(legacy.MicrophoneVolumeDb == 0, "Legacy calibration volume default is incorrect.");

    string percentPath = Path.Combine(root, "percent-settings.json");
    File.WriteAllText(percentPath, "{\"Setting\":{\"MicrophoneVolumePercent\":200}}");
    Assert(TUFReplaySetting.Load(percentPath).MicrophoneVolumeDb == 6, "Legacy percent volume was not migrated.");

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
    Assert(microphone[240] > 0.99f, "Microphone waveform did not apply the capture start offset.");
    Assert(microphone[0] == 0f, "Microphone waveform leaked before its capture start.");

    float[] game = CalibrationWaveformBuilder.FromTimedPeaks(
      new[] { 0.5f, 1f },
      new long[] { 100, 600 },
      2,
      1000,
      1000d
    );
    Assert(game.Length == CalibrationWaveformBuilder.BinCount, "Game waveform bin count is wrong.");
    Assert(Math.Abs(game[10] - 0.5f) < 0.001f, "Game waveform peak normalization is wrong.");
    Assert(game[200] > 0.99f && game[400] == 0f, "Game waveform timing aggregation is wrong.");
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
      Assert(Convert.ToInt32(version.ExecuteScalar()) == 8, "Fresh schema version is not 8.");
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
    MicrophoneRecordingRepository.Save(recording);

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
      setup.CommandText = "DROP TABLE microphone_recordings; PRAGMA user_version=7; PRAGMA foreign_keys=ON;";
      setup.ExecuteNonQuery();
    }
    ActivitySchema.Ensure(migration);
    using SqliteCommand migrated = migration.CreateCommand();
    migrated.CommandText = "SELECT user_version FROM pragma_user_version";
    Assert(Convert.ToInt32(migrated.ExecuteScalar()) == 8, "Schema 7 to 8 migration failed.");
  }

  private static void InsertRun(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes) VALUES('app','2026-01-01',0);
INSERT INTO level_sessions(id,app_session_id,level_path,opened_at_utc) VALUES('level','app','test.adofai','2026-01-01');
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
