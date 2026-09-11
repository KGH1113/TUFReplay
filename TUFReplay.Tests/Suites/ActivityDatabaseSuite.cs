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

internal static class ActivityDatabaseSuite
{
  internal static void RunAll(string root)
  {
    TestTufLevelIdResolverCache();
    TestGameplayChartHashVersioning();
    TestGameplayHashIdentityUpgrade(root);
    TestGameplayHashV3Migration(root);
    TestLegacyInputSchemaAdvancePreservesBlob(root);
    TestSchemaMigrationAndBlob(root);
    TestLegacyReplayDetection(root);
    TestAppSessionTransientLockRecovery(root);
    TestBrokenRenamedForeignKeyRepair(root);
    TestRunDeletionHierarchy(root);
    TestLogicalRunSessionFilter(root);
    TestLogicalLevelIdentity(root);
    TestQualifiedClearCounts(root);
  }

  private static void TestLegacyInputSchemaAdvancePreservesBlob(string root)
  {
    string path = Path.Combine(root, "legacy-input-schema.sqlite");
    using var connection = new SqliteConnection("Data Source=" + path);
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      "CREATE TABLE runs(input_csv BLOB NOT NULL); INSERT INTO runs VALUES(X'010203FF'); PRAGMA user_version=6;";
    command.ExecuteNonQuery();

    ActivitySchema.AdvanceLegacyInputSchemaWithoutMutation(connection);
    command.CommandText = "SELECT input_csv FROM runs";
    byte[] preserved = (byte[])command.ExecuteScalar();
    Assert(preserved.SequenceEqual(new byte[] { 1, 2, 3, 255 }), "Schema advance modified a legacy input blob.");
    command.CommandText = "PRAGMA user_version";
    Assert(Convert.ToInt32(command.ExecuteScalar()) == 7, "Legacy input schema version did not advance.");
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
    Assert(
      RunRepository.Get("run")?.SubmissionRunId == "68727984-2424-4a6d-a72b-919044143454",
      "Activity run did not preserve its linked submission run."
    );

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

  private static void TestLegacyReplayDetection(string root)
  {
    string path = Path.Combine(root, "legacy-replay-detection.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand command = connection.CreateCommand();
      command.CommandText =
        @"INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes)
VALUES('legacy-app','2026-01-01',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc)
VALUES('legacy-level','legacy-detection',0,'legacy.adofai','2026-01-01','2026-01-01');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc)
VALUES('legacy-session','legacy-level','legacy-app','2026-01-01');";
      command.ExecuteNonQuery();

      Assert(!RunRepository.HasLegacyReplay(), "An empty database reported a legacy replay.");

      command.CommandText =
        @"INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,start_tile,result,input_count,hit_context_count,meta_json
) VALUES('empty-run','legacy-session',0,'2026-01-01',0,'quit',0,0,'{broken');";
      command.ExecuteNonQuery();
      Assert(!RunRepository.HasLegacyReplay(), "An empty run was treated as a replay.");

      string currentMetadata = new RecordedRunPayload { InputCapture = "test-native-capture" }.ToActivityMetaJson();
      command.CommandText =
        @"INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,start_tile,result,input_count,hit_context_count,meta_json
) VALUES('format-run','legacy-session',1,'2026-01-01',0,'quit',1,0,@meta);";
      command.Parameters.AddWithValue("@meta", currentMetadata);
      command.ExecuteNonQuery();
      Assert(!RunRepository.HasLegacyReplay(), "A current native-input replay was treated as legacy.");

      command.CommandText = "UPDATE runs SET meta_json='{\"formatVersion\":2}' WHERE id='format-run'";
      command.Parameters.Clear();
      command.ExecuteNonQuery();
      Assert(RunRepository.HasLegacyReplay(), "A format-version 2 replay was not treated as legacy.");

      string oldCaptureMetadata = new RecordedRunPayload
      {
        InputCapture = "skyhook-native-events",
      }.ToActivityMetaJson();
      command.CommandText = "UPDATE runs SET meta_json=@meta WHERE id='format-run'";
      command.Parameters.AddWithValue("@meta", oldCaptureMetadata);
      command.ExecuteNonQuery();
      Assert(RunRepository.HasLegacyReplay(), "An old capture-engine replay was not treated as legacy.");

      command.CommandText = "UPDATE runs SET meta_json='{broken' WHERE id='format-run'";
      command.Parameters.Clear();
      command.ExecuteNonQuery();
      Assert(RunRepository.HasLegacyReplay(), "Malformed replay metadata was not treated as legacy.");
    }
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
    byte[] v4Before = ComputeVersion4Hash(100f, "song.ogg", 100, 0, 100, 13);
    Assert(
      v1Before.Length == 16 && v2Before.Length == 32 && v3Before.Length == 32 && v4Before.Length == 32,
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
    Assert(
      !GameplayChartHash.Equals(v3Before, ComputeVersion3Hash(100f, "song.ogg", 100, 0, 100, 19)),
      "Legacy v3 unexpectedly stopped preserving the level format version."
    );
    Assert(
      GameplayChartHash.Equals(v4Before, ComputeVersion4Hash(100f, "song.ogg", 70, 1, 40, 19)),
      "Level format version, pitch, or hit-sound presentation changed the v4 chart identity."
    );
    Assert(
      !GameplayChartHash.Equals(v4Before, ComputeVersion4Hash(101f, "song.ogg", 100, 0, 100, 13)),
      "Gameplay BPM did not change the v4 hash."
    );
    Assert(GameplayChartHash.IsSupported(1, v1Before), "Legacy v1 hash is not supported.");
    Assert(GameplayChartHash.IsSupported(2, v2Before), "Legacy v2 hash is not supported.");
    Assert(GameplayChartHash.IsSupported(3, v3Before), "Legacy v3 hash is not supported.");
    Assert(GameplayChartHash.IsSupported(4, v4Before), "Current v4 hash is not supported.");
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

    byte[] currentHash = new byte[GameplayChartHash.Version4Size];
    currentHash[0] = 4;
    var currentLevel = new LevelRecord
    {
      Id = "current-hash-level",
      SourceKind = LevelSourceKind.Local,
      LevelPath = legacyLevel.LevelPath,
      GameplayHash = currentHash,
      GameplayHashVersion = GameplayChartHash.Version,
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
    byte[] currentHash = ComputeVersion4Hash(130f, "song.ogg", 100, 0, 100, 19);
    byte[] version3Hash = ComputeVersion3Hash(130f, "song.ogg", 100, 0, 100, 15);
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
    string v3Session = SaveLegacyGameplayLevel(chartPath, version3Hash, 3, 100, "beta8-v3");

    byte[] unmatchedHash = new byte[GameplayChartHash.Version2Size];
    unmatchedHash[0] = 0x7f;
    string unmatchedSession = SaveLegacyGameplayLevel(chartPath, unmatchedHash, 2, 120, "unmatched");
    string unmatchedLevelId = LevelSessionRepository.Get(unmatchedSession)?.LevelId;

    GameplayHashV3MigrationResult migrated = GameplayHashV3Migration.Run(
      (_, legacyHash, _, _) => GameplayChartHash.Equals(legacyHash, unmatchedHash) ? null : currentHash
    );
    Assert(migrated.Scanned == 6, "Gameplay hash migration did not scan every v1-v3 level.");
    Assert(migrated.Migrated == 5, "Gameplay hash migration did not merge every verified legacy level.");
    Assert(migrated.Deferred == 1, "Gameplay hash migration did not defer the unverifiable level.");

    string migratedLevelId = LevelSessionRepository.Get(v1Session)?.LevelId;
    Assert(!string.IsNullOrWhiteSpace(migratedLevelId), "Migrated beta6 level session disappeared.");
    Assert(
      LevelSessionRepository.Get(v2Pitch100Session)?.LevelId == migratedLevelId
        && LevelSessionRepository.Get(v2Pitch150Session)?.LevelId == migratedLevelId
        && LevelSessionRepository.Get(v2QuietSession)?.LevelId == migratedLevelId
        && LevelSessionRepository.Get(v3Session)?.LevelId == migratedLevelId,
      "Verified v1-v3 gameplay identities were not merged."
    );
    using (SqliteConnection connection = Database.OpenConnection())
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText = "SELECT gameplay_hash,gameplay_hash_version FROM levels WHERE id=@id";
      command.Parameters.AddWithValue("@id", migratedLevelId);
      using (SqliteDataReader reader = command.ExecuteReader())
      {
        Assert(reader.Read(), "Merged v4 level disappeared.");
        Assert(
          GameplayChartHash.Equals((byte[])reader.GetValue(0), currentHash)
            && reader.GetInt32(1) == GameplayChartHash.Version,
          "Merged legacy levels did not receive the v4 gameplay hash."
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
    int hitsoundVolume,
    int levelVersion = 15
  )
  {
    _ = pitch;
    _ = hitsound;
    _ = hitsoundVolume;
    using var writer = new GameplayChartHashCanonicalWriter();
    writer.WriteFormatVersion(3);
    writer.WriteGameplaySettingsV3(levelVersion, songFilename, bpm, 100, 0, false, 4, 0f, false);
    writer.WriteChartKind(false);
    writer.WriteAngles(new[] { 0f, 90f, 180f });
    return writer.ComputeSha256Hash();
  }

  private static byte[] ComputeVersion4Hash(
    float bpm,
    string songFilename,
    int pitch,
    byte hitsound,
    int hitsoundVolume,
    int levelVersion
  )
  {
    _ = pitch;
    _ = hitsound;
    _ = hitsoundVolume;
    _ = levelVersion;
    using var writer = new GameplayChartHashCanonicalWriter();
    writer.WriteFormatVersion(4);
    writer.WriteGameplaySettingsV4(songFilename, bpm, 100, 0, false, 4, 0f, false);
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

    string tufGroupA = LevelGroupIdentity.Create(77, "/tmp/tuf-a.adofai");
    string tufGroupB = LevelGroupIdentity.Create(77, "/tmp/tuf-b.adofai");
    string otherTufGroup = LevelGroupIdentity.Create(78, "/tmp/tuf-a.adofai");
    Assert(tufGroupA == tufGroupB, "TUF revisions with the same level ID were not grouped.");
    Assert(tufGroupA != otherTufGroup, "Different TUF level IDs shared a group ID.");

    string localPath = Path.Combine(root, "group", "local.adofai");
    string equivalentLocalPath = Path.Combine(root, "group", "nested", "..", "local.adofai");
    string localGroupA = LevelGroupIdentity.Create(null, localPath);
    string localGroupB = LevelGroupIdentity.Create(null, equivalentLocalPath);
    string otherLocalGroup = LevelGroupIdentity.Create(null, Path.Combine(root, "group", "other.adofai"));
    Assert(localGroupA == localGroupB, "Equivalent local paths were not grouped.");
    Assert(localGroupA != otherLocalGroup, "Different local paths shared a group ID.");
    Assert(!localGroupA.Contains(localPath), "The local level path leaked into the group ID.");
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

  private static void TestQualifiedClearCounts(string root)
  {
    SetDatabasePath(Path.Combine(root, "qualified-clear-counts.sqlite"));
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      using SqliteCommand seed = connection.CreateCommand();
      seed.CommandText =
        @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes)
VALUES('clear-app','2026-01-01',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,level_tile_count,first_seen_at_utc,last_seen_at_utc)
VALUES('clear-level','clear-identity',0,'clear.adofai',100,'2026-01-01','2026-01-01');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc)
VALUES('clear-session','clear-level','clear-app','2026-01-01');
INSERT INTO runs(id,level_session_id,run_index,started_at_utc,start_tile,last_tile,result,no_fail_mode)
VALUES
  ('qualified','clear-session',0,'2026-01-01T00:00:00Z',0,100,'cleared',0),
  ('legacy-completed','clear-session',1,'2026-01-01T00:01:00Z',0,100,'completed',0),
  ('checkpoint','clear-session',2,'2026-01-01T00:02:00Z',40,100,'cleared',0),
  ('no-fail','clear-session',3,'2026-01-01T00:03:00Z',0,100,'cleared',1),
  ('rounded-only','clear-session',4,'2026-01-01T00:04:00Z',0,99,'failed',0);";
      seed.ExecuteNonQuery();
    }

    LevelSessionOverview session = ActivityRepository.GetLevelSessionOverview("clear-session");
    LogicalLevelOverview level = ActivityRepository.GetLogicalLevelOverview("clear-level");
    Assert(session.ClearRunCount == 2, "Level-session clear count included an unqualified run.");
    Assert(level.ClearRunCount == 2, "Logical-level clear count included an unqualified run.");
  }

  private static void InsertRun(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes) VALUES('app','2026-01-01',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc) VALUES('logical','test',0,'test.adofai','2026-01-01','2026-01-01');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc) VALUES('level','logical','app','2026-01-01');
INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,start_tile,result,judgment_difficulty,no_fail_mode,submission_run_id
) VALUES(
  'run','level',0,'2026-01-01',0,'cleared',1,1,'68727984-2424-4a6d-a72b-919044143454'
);";
    command.ExecuteNonQuery();
  }

  private static void SetDatabasePath(string path)
  {
    PropertyInfo property = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    property.SetValue(null, path);
    MicrophoneDatabase.Initialize(
      Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".microphones.sqlite")
    );
  }

  private static void TestTufLevelIdResolverCache()
  {
    int now = 1000;
    int factoryCalls = 0;
    int resolverCalls = 0;
    var cache = new TufLevelIdResolverCache(
      () =>
      {
        factoryCalls++;
        return path =>
        {
          resolverCalls++;
          return path == "/levels/known.adofai" ? 42 : null;
        };
      },
      null,
      50,
      () => now
    );

    Assert(cache.Resolve("/levels/known.adofai") == 42, "TUF level resolver did not return a positive result.");
    Assert(cache.Resolve("/levels/known.adofai") == 42, "Cached TUF level result changed.");
    Assert(factoryCalls == 1 && resolverCalls == 1, "Positive TUF level resolution was not cached.");

    Assert(cache.Resolve("/levels/missing.adofai") == null, "Missing TUF level unexpectedly resolved.");
    Assert(cache.Resolve("/levels/missing.adofai") == null, "Negative TUF level cache changed its result.");
    Assert(resolverCalls == 2, "Negative TUF level resolution was repeated before its TTL.");
    now += 50;
    Assert(cache.Resolve("/levels/missing.adofai") == null, "Expired negative TUF cache changed its result.");
    Assert(resolverCalls == 3, "Negative TUF level resolution did not retry after its TTL.");

    bool resolverAvailable = false;
    factoryCalls = 0;
    var lateResolver = new TufLevelIdResolverCache(
      () =>
      {
        factoryCalls++;
        return resolverAvailable ? _ => 99 : null;
      },
      null,
      50,
      () => now
    );
    Assert(lateResolver.Resolve("/levels/late.adofai") == null, "Unavailable TUF resolver returned a value.");
    resolverAvailable = true;
    Assert(lateResolver.Resolve("/levels/other.adofai") == null, "Resolver lookup ignored its negative TTL.");
    Assert(factoryCalls == 1, "Unavailable TUF resolver reflection was repeated before its TTL.");
    now += 50;
    Assert(lateResolver.Resolve("/levels/late.adofai") == 99, "Late TUF resolver was not discovered after TTL.");
    Assert(factoryCalls == 2, "Late TUF resolver discovery did not retry exactly once.");
  }
}
