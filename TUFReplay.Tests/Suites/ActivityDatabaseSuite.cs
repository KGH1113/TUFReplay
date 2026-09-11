using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Database;
using static TestFixture;

internal static class ActivityDatabaseSuite
{
  internal static void RunAll(string root)
  {
    TestFreshSchemaAndAtomicArtifact(root);
    TestLegacyV15Import(root);
    TestUnsupportedAndFailedImportsPreserveSource(root);
    TestInterruptedReplacementRecovery(root);
  }

  private static void TestFreshSchemaAndAtomicArtifact(string root)
  {
    string path = Path.Combine(root, "fresh-activity.sqlite");
    SetDatabasePath(path);
    using (SqliteConnection connection = Database.OpenConnection())
    {
      ActivitySchema.Ensure(connection);
      ActivitySchema.Validate(connection);
      ActivitySchema.ReadHeader(connection, out int applicationId, out int version);
      Assert(applicationId == ActivitySchema.ApplicationId, "Fresh activity database application ID is incorrect.");
      Assert(version == ActivitySchema.Version, "Fresh activity database schema version is incorrect.");
      Assert(
        string.Join(",", ReadUserTables(connection)) == "app_sessions,level_sessions,levels,replay_artifacts,runs",
        "Fresh activity database contains an unexpected table."
      );
      InsertParents(connection);
    }

    RunRepository.Save(CreateRun("run", 0, CreateArtifact()));
    StoredReplayRun stored = RunRepository.GetReplayRun("run");
    Assert(stored != null, "Saved replay run was not found.");
    Assert(stored.EngineId == ReplayFormat.EngineId, "Replay engine ID did not round-trip.");
    Assert(stored.FormatVersion == ReplayFormat.FormatVersion, "Replay format version did not round-trip.");
    Assert(stored.InputCsv.SequenceEqual(new byte[] { 1, 2, 3 }), "Replay input payload changed.");
    Assert(stored.HitContextCsv.SequenceEqual(new byte[] { 4, 5 }), "Replay hit payload changed.");
    Assert(stored.MetaJson == "{\"v\":1}", "Replay metadata changed.");
    Assert(RunRepository.Get("run")?.ReplayPlayable == true, "Current replay artifact was not marked playable.");

    RunRecord invalid = CreateRun("atomic-failure", 1, CreateArtifact());
    invalid.ReplayArtifact.EngineId = null;
    AssertThrows<Exception>(() => RunRepository.Save(invalid), "Invalid replay artifact unexpectedly saved.");
    Assert(!RunRepository.Exists("atomic-failure"), "Failed artifact insert left its activity run behind.");
  }

  private static void TestLegacyV15Import(string root)
  {
    string directory = Path.Combine(root, "v15-import");
    Directory.CreateDirectory(directory);
    string main = Path.Combine(directory, "tufreplay.sqlite");
    string migrating = Path.Combine(directory, "tufreplay.0.2.migrating.sqlite");
    string backup = Path.Combine(directory, "tufreplay.pre-0.2.sqlite");
    CreateLegacyV15Database(main);
    SetDatabasePath(main);

    LegacyActivityV15Importer.EnsureCurrent(main, migrating, backup);
    Assert(File.Exists(main) && File.Exists(backup), "Successful import did not retain both current DB and backup.");
    Assert(!File.Exists(migrating), "Successful import left the migrating DB behind.");

    using (SqliteConnection connection = Database.OpenConnection())
    {
      Assert(ActivitySchema.IsCurrent(connection), "Imported activity database does not have the current header.");
      ActivitySchema.Validate(connection);
      using SqliteCommand command = connection.CreateCommand();
      command.CommandText =
        "SELECT result,input_count,hit_context_count,replay_unavailable_reason FROM runs WHERE id='legacy-run'";
      using SqliteDataReader run = command.ExecuteReader();
      Assert(run.Read(), "Imported run ID was not preserved.");
      Assert(
        run.GetString(0) == "cleared"
          && run.GetInt32(1) == 2
          && run.GetInt32(2) == 1
          && run.GetString(3) == ReplayUnavailableReasons.LegacyEngine,
        "Imported run statistics or replay policy changed."
      );
      run.Close();
      command.CommandText = "SELECT gameplay_hash,gameplay_hash_version FROM levels WHERE id='legacy-level'";
      using SqliteDataReader level = command.ExecuteReader();
      Assert(level.Read() && level.IsDBNull(0) && level.IsDBNull(1), "Legacy gameplay hash was imported.");
      level.Close();
      command.CommandText = "SELECT count(*) FROM replay_artifacts";
      Assert(Convert.ToInt32(command.ExecuteScalar()) == 0, "Legacy replay payload was imported as a v2 artifact.");
    }

    using (SqliteConnection connection = OpenUnpooled(backup))
    {
      using SqliteCommand command = connection.CreateCommand();
      command.CommandText = "SELECT input_csv,hit_context_csv,meta_json FROM runs WHERE id='legacy-run'";
      using SqliteDataReader payload = command.ExecuteReader();
      Assert(
        payload.Read()
          && ((byte[])payload.GetValue(0)).SequenceEqual(new byte[] { 1, 2, 3 })
          && ((byte[])payload.GetValue(1)).SequenceEqual(new byte[] { 4, 5 }),
        "Pre-0.2 backup did not preserve the legacy replay payload."
      );
    }

    Assert(
      MicrophoneRecordingRepository.ImportEmbeddedLegacyRecordings(backup) == 1,
      "Embedded microphone was not imported."
    );
    Assert(
      MicrophoneRecordingRepository.ImportEmbeddedLegacyRecordings(backup) == 0,
      "Completed microphone import was repeated."
    );
    Assert(MicrophoneRecordingRepository.Exists("legacy-run"), "Imported microphone recording is missing.");
    using (SqliteConnection microphone = MicrophoneDatabase.OpenConnection())
    using (SqliteCommand command = microphone.CreateCommand())
    {
      command.CommandText = "SELECT audio_wav FROM microphone_recordings WHERE run_id='legacy-run'";
      Assert(
        ((byte[])command.ExecuteScalar()).SequenceEqual(new byte[] { 9, 8, 7, 6 }),
        "Streamed microphone import changed the BLOB."
      );
    }
    Assert(MicrophoneRecordingRepository.DeleteOrphans() == 0, "Valid imported microphone was treated as orphaned.");
    Assert(RunRepository.Delete("legacy-run"), "Imported run could not be deleted.");
    Assert(MicrophoneRecordingRepository.DeleteOrphans() == 1, "Deleted imported run left an orphaned microphone.");
    Assert(
      MicrophoneRecordingRepository.ImportEmbeddedLegacyRecordings(backup) == 0
        && !MicrophoneRecordingRepository.Exists("legacy-run"),
      "Deleted imported microphone was resurrected from the backup."
    );
    Assert(CountRows(main, "level_sessions") == 0, "Closed orphan level session was not cleaned up.");
    Assert(CountRows(main, "app_sessions") == 0, "Closed orphan app session was not cleaned up.");
  }

  private static void TestUnsupportedAndFailedImportsPreserveSource(string root)
  {
    string unsupported = Path.Combine(root, "unsupported-v14.sqlite");
    CreateLegacyV15Database(unsupported, 14);
    AssertThrows<InvalidOperationException>(
      () => LegacyActivityV15Importer.EnsureCurrent(unsupported, unsupported + ".migrating", unsupported + ".backup"),
      "Unsupported v14 database was silently initialized."
    );
    Assert(ReadUserVersion(unsupported) == 14, "Unsupported database was modified.");
    Assert(CountRows(unsupported, "runs") == 1, "Unsupported database lost activity data.");

    string corrupt = Path.Combine(root, "corrupt.sqlite");
    byte[] corruptBytes = { 1, 3, 3, 7, 9 };
    File.WriteAllBytes(corrupt, corruptBytes);
    AssertThrows<SqliteException>(
      () => LegacyActivityV15Importer.EnsureCurrent(corrupt, corrupt + ".migrating", corrupt + ".backup"),
      "Corrupt database was silently initialized."
    );
    Assert(File.ReadAllBytes(corrupt).SequenceEqual(corruptBytes), "Corrupt source database was overwritten.");

    string collision = Path.Combine(root, "backup-collision.sqlite");
    string collisionBackup = collision + ".backup";
    CreateLegacyV15Database(collision);
    File.WriteAllBytes(collisionBackup, new byte[] { 7, 7, 7 });
    AssertThrows<IOException>(
      () => LegacyActivityV15Importer.EnsureCurrent(collision, collision + ".migrating", collisionBackup),
      "Existing backup was overwritten."
    );
    Assert(ReadUserVersion(collision) == 15, "Backup collision modified the source database.");
    Assert(File.ReadAllBytes(collisionBackup).SequenceEqual(new byte[] { 7, 7, 7 }), "Existing backup changed.");

    string invalidImport = Path.Combine(root, "invalid-import.sqlite");
    CreateLegacyV15Database(invalidImport);
    using (SqliteConnection connection = OpenUnpooled(invalidImport))
    {
      using SqliteCommand command = connection.CreateCommand();
      command.CommandText = "PRAGMA foreign_keys=OFF; UPDATE runs SET level_session_id='missing';";
      command.ExecuteNonQuery();
    }
    AssertThrows<SqliteException>(
      () =>
        LegacyActivityV15Importer.EnsureCurrent(invalidImport, invalidImport + ".migrating", invalidImport + ".backup"),
      "Invalid v15 import unexpectedly succeeded."
    );
    Assert(File.Exists(invalidImport) && ReadUserVersion(invalidImport) == 15, "Failed import replaced its source.");
    Assert(!File.Exists(invalidImport + ".backup"), "Failed import created a source backup.");
  }

  private static void TestInterruptedReplacementRecovery(string root)
  {
    string completeDirectory = Path.Combine(root, "replacement-complete");
    Directory.CreateDirectory(completeDirectory);
    string completeMain = Path.Combine(completeDirectory, "main.sqlite");
    string completeMigrating = Path.Combine(completeDirectory, "migrating.sqlite");
    string completeBackup = Path.Combine(completeDirectory, "backup.sqlite");
    CreateLegacyV15Database(completeBackup);
    CreateCurrentDatabase(completeMigrating);
    LegacyActivityV15Importer.EnsureCurrent(completeMain, completeMigrating, completeBackup);
    Assert(File.Exists(completeMain) && File.Exists(completeBackup), "Interrupted replacement was not completed.");
    Assert(!File.Exists(completeMigrating), "Completed replacement retained the migrating file.");
    Assert(IsCurrentDatabase(completeMain), "Recovered main database is not current.");

    string restoreDirectory = Path.Combine(root, "replacement-restore");
    Directory.CreateDirectory(restoreDirectory);
    string restoreMain = Path.Combine(restoreDirectory, "main.sqlite");
    string restoreMigrating = Path.Combine(restoreDirectory, "migrating.sqlite");
    string restoreBackup = Path.Combine(restoreDirectory, "backup.sqlite");
    CreateLegacyV15Database(restoreBackup);
    File.WriteAllBytes(restoreMigrating, new byte[] { 0, 1, 2 });
    AssertThrows<InvalidOperationException>(
      () => LegacyActivityV15Importer.EnsureCurrent(restoreMain, restoreMigrating, restoreBackup),
      "Invalid migrating DB recovery did not stop initialization."
    );
    Assert(
      File.Exists(restoreMain) && ReadUserVersion(restoreMain) == 15,
      "Invalid migrating DB did not restore backup."
    );
    Assert(!File.Exists(restoreMigrating), "Invalid migrating DB was not removed.");
  }

  private static RunRecord CreateRun(string id, int index, ReplayArtifact artifact)
  {
    return new RunRecord
    {
      Id = id,
      LevelSessionId = "level-session",
      RunIndex = index,
      StartedAtUtc = "2026-01-01T00:00:00Z",
      EndedAtUtc = "2026-01-01T00:01:00Z",
      Result = "cleared",
      NoFailMode = true,
      JudgmentDifficulty = RunJudgmentDifficulty.Normal,
      InputCount = artifact.InputCount,
      HitContextCount = artifact.HitContextCount,
      ReplayArtifact = artifact,
    };
  }

  private static ReplayArtifact CreateArtifact()
  {
    return new ReplayArtifact
    {
      EngineId = ReplayFormat.EngineId,
      FormatVersion = ReplayFormat.FormatVersion,
      InputCount = 1,
      HitContextCount = 1,
      InputCsv = new byte[] { 1, 2, 3 },
      HitContextCsv = new byte[] { 4, 5 },
      MetadataJson = "{\"v\":1}",
    };
  }

  private static void InsertParents(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"INSERT INTO app_sessions(id,started_at_utc,recorder_utc_offset_minutes)
VALUES('app','2026-01-01T00:00:00Z',0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc)
VALUES('level','local:test',0,'test.adofai','2026-01-01T00:00:00Z','2026-01-01T00:00:00Z');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc)
VALUES('level-session','level','app','2026-01-01T00:00:00Z');";
    command.ExecuteNonQuery();
  }

  private static void CreateLegacyV15Database(string path, int version = 15)
  {
    using SqliteConnection connection = OpenUnpooled(path);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"PRAGMA foreign_keys=ON;
CREATE TABLE app_sessions(
  id TEXT PRIMARY KEY,started_at_utc TEXT NOT NULL,ended_at_utc TEXT,
  recorder_time_zone_id TEXT,recorder_utc_offset_minutes INTEGER NOT NULL
);
CREATE TABLE levels(
  id TEXT PRIMARY KEY,identity_key TEXT NOT NULL UNIQUE,source_kind INTEGER NOT NULL,tuf_level_id INTEGER,
  adofai_path TEXT NOT NULL,gameplay_hash BLOB,gameplay_hash_version INTEGER,level_tile_count INTEGER NOT NULL DEFAULT 0,
  song TEXT,author TEXT,artist TEXT,metadata_state INTEGER NOT NULL DEFAULT 0,
  first_seen_at_utc TEXT NOT NULL,last_seen_at_utc TEXT NOT NULL
);
CREATE TABLE level_sessions(
  id TEXT PRIMARY KEY,level_id TEXT NOT NULL REFERENCES levels(id),app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  opened_at_utc TEXT NOT NULL,closed_at_utc TEXT
);
CREATE TABLE runs(
  id TEXT PRIMARY KEY,level_session_id TEXT NOT NULL REFERENCES level_sessions(id),run_index INTEGER NOT NULL,
  started_at_utc TEXT NOT NULL,ended_at_utc TEXT,start_tile INTEGER NOT NULL DEFAULT 0,last_tile INTEGER,
  result TEXT NOT NULL DEFAULT 'unknown',no_fail_mode INTEGER NOT NULL DEFAULT 0,
  gameplay_start_song_position REAL,level_pitch_percent INTEGER,effective_pitch REAL,x_accuracy REAL,
  judgment_difficulty INTEGER,judgment_overload INTEGER NOT NULL DEFAULT 0,judgment_too_early INTEGER NOT NULL DEFAULT 0,
  judgment_early INTEGER NOT NULL DEFAULT 0,judgment_early_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_perfect INTEGER NOT NULL DEFAULT 0,judgment_late_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late INTEGER NOT NULL DEFAULT 0,judgment_too_late INTEGER NOT NULL DEFAULT 0,
  judgment_miss INTEGER NOT NULL DEFAULT 0,input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',meta_json TEXT NOT NULL DEFAULT '{}'
);
CREATE TABLE microphone_recordings(
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,audio_wav BLOB NOT NULL,format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,channels INTEGER NOT NULL,frame_count INTEGER NOT NULL,device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0,is_permanent INTEGER NOT NULL DEFAULT 0,expires_at_utc TEXT
);
INSERT INTO app_sessions VALUES('legacy-app','2026-01-01T00:00:00Z','2026-01-01T01:00:00Z','UTC',0);
INSERT INTO levels VALUES(
  'legacy-level','old-identity',0,NULL,'legacy.adofai',X'01020304',3,100,'Song','Author','Artist',1,
  '2026-01-01T00:00:00Z','2026-01-01T00:00:00Z'
);
INSERT INTO level_sessions VALUES(
  'legacy-session','legacy-level','legacy-app','2026-01-01T00:00:00Z','2026-01-01T01:00:00Z'
);
INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_perfect,input_count,hit_context_count,input_csv,hit_context_csv,meta_json
) VALUES(
  'legacy-run','legacy-session',0,'2026-01-01T00:00:00Z','2026-01-01T00:01:00Z',0,100,'cleared',0,
  0.0,100,1.0,0.99,1,1,2,1,X'010203',X'0405','{""legacy"":true}'
);
INSERT INTO microphone_recordings VALUES(
  'legacy-run',X'09080706','wav/pcm16',48000,1,2,'legacy-device',123,1,NULL
);";
    command.ExecuteNonQuery();
    command.CommandText = "PRAGMA user_version=" + version;
    command.ExecuteNonQuery();
  }

  private static void CreateCurrentDatabase(string path)
  {
    using SqliteConnection connection = OpenUnpooled(path);
    ActivitySchema.Ensure(connection);
    ActivitySchema.Validate(connection);
  }

  private static bool IsCurrentDatabase(string path)
  {
    using SqliteConnection connection = OpenUnpooled(path);
    return ActivitySchema.IsCurrent(connection);
  }

  private static string[] ReadUserTables(SqliteConnection connection)
  {
    var result = new List<string>();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
      result.Add(reader.GetString(0));
    return result.ToArray();
  }

  private static int ReadUserVersion(string path)
  {
    using SqliteConnection connection = OpenUnpooled(path);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA user_version";
    return Convert.ToInt32(command.ExecuteScalar());
  }

  private static int CountRows(string path, string table)
  {
    using SqliteConnection connection = OpenUnpooled(path);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT count(*) FROM " + table;
    return Convert.ToInt32(command.ExecuteScalar());
  }

  private static SqliteConnection OpenUnpooled(string path)
  {
    var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False");
    connection.Open();
    return connection;
  }

  private static void SetDatabasePath(string path)
  {
    PropertyInfo property = typeof(Database).GetProperty("DbPath", BindingFlags.Public | BindingFlags.Static);
    property.SetValue(null, path);
    MicrophoneDatabase.Initialize(
      Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".microphones.sqlite")
    );
  }
}
