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
    TestLegacyDatabaseReset(root);
    TestUnsupportedDatabasesArePreserved(root);
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

    Assert(!RunRepository.HasLegacyReplay(), "An empty database reported a legacy replay.");
    RunRepository.Save(CreateRun("run", 0, CreateArtifact()));
    StoredReplayRun stored = RunRepository.GetReplayRun("run");
    Assert(stored != null, "Saved replay run was not found.");
    Assert(stored.EngineId == ReplayFormat.EngineId, "Replay engine ID did not round-trip.");
    Assert(stored.FormatVersion == ReplayFormat.FormatVersion, "Replay format version did not round-trip.");
    Assert(stored.InputCsv.SequenceEqual(new byte[] { 1, 2, 3 }), "Replay input payload changed.");
    Assert(stored.HitContextCsv.SequenceEqual(new byte[] { 4, 5 }), "Replay hit payload changed.");
    Assert(stored.MetaJson == "{\"v\":1}", "Replay metadata changed.");
    Assert(RunRepository.Get("run")?.ReplayPlayable == true, "Current replay artifact was not marked playable.");
    RunRecord activityRun = RunRepository.Get("run");
    Assert(
      activityRun?.JudgmentSystem == RunJudgmentSystem.ModernCompetitive,
      "Run judgment system did not round-trip."
    );
    Assert(
      activityRun.JudgmentCounts.PerfectMinus == 1
        && activityRun.JudgmentCounts.XPerfect == 2
        && activityRun.JudgmentCounts.PerfectPlus == 3,
      "Competitive judgment counts did not round-trip."
    );
    Assert(!RunRepository.HasLegacyReplay(), "A current replay artifact was treated as legacy.");

    using (SqliteConnection connection = Database.OpenConnection())
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.CommandText =
        @"INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,result,input_count,hit_context_count,replay_unavailable_reason
) VALUES('empty-legacy','level-session',2,'2026-01-01T00:00:00Z','quit',0,0,'legacy_engine')";
      command.ExecuteNonQuery();
      Assert(!RunRepository.HasLegacyReplay(), "An empty run was treated as a legacy replay.");
      command.CommandText = "UPDATE runs SET hit_context_count=1 WHERE id='empty-legacy'";
      command.ExecuteNonQuery();
    }
    Assert(RunRepository.HasLegacyReplay(), "A legacy replay was not detected.");

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
    using (SqliteConnection walConnection = OpenUnpooled(main))
    using (SqliteCommand walCommand = walConnection.CreateCommand())
    {
      walCommand.CommandText = "PRAGMA journal_mode=WAL";
      Assert(
        string.Equals(Convert.ToString(walCommand.ExecuteScalar()), "wal"),
        "Legacy test database did not enter WAL mode."
      );
    }
    string warning = null;

    LegacyActivityDatabaseTransition.EnsureCurrent(main, migrating, backup, message => warning = message);

    Assert(IsCurrentDatabase(main), "Schema v15 was not imported into the current database.");
    Assert(File.Exists(backup) && ReadUserVersion(backup) == 15, "Schema v15 source backup was not preserved.");
    Assert(CountRows(main, "runs") == 1, "Schema v15 activity run was not imported.");
    Assert(warning == null, "Schema v15 import emitted a destructive-reset warning.");
    using SqliteConnection connection = OpenUnpooled(main);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT replay_unavailable_reason FROM runs WHERE id='legacy-run'";
    Assert(
      string.Equals(Convert.ToString(command.ExecuteScalar()), ReplayUnavailableReasons.LegacyEngine),
      "Schema v15 replay was not marked as legacy."
    );

    SetDatabasePath(main);
    Assert(
      MicrophoneRecordingRepository.ImportEmbeddedLegacyRecordings(backup) == 1,
      "Embedded microphone recording was not imported from a WAL-mode v15 backup."
    );
    Assert(
      CountRows(MicrophoneDatabase.DbPath, "microphone_recordings") == 1,
      "Imported microphone recording was not stored in the dedicated database."
    );
  }

  private static void TestLegacyDatabaseReset(string root)
  {
    string directory = Path.Combine(root, "legacy-reset");
    Directory.CreateDirectory(directory);
    for (int version = 0; version <= LegacyActivityDatabaseTransition.MaximumDiscardedVersion; version++)
    {
      string path = Path.Combine(directory, "v" + version + ".sqlite");
      CreateLegacyV15Database(path, version);
      string warning = null;

      LegacyActivityDatabaseTransition.EnsureCurrent(
        path,
        path + ".migrating",
        path + ".backup",
        message => warning = message
      );

      Assert(IsCurrentDatabase(path), "Legacy schema v" + version + " was not replaced with the current schema.");
      Assert(CountRows(path, "runs") == 0, "Legacy schema v" + version + " retained activity runs.");
      Assert(
        warning != null && warning.Contains("schema v" + version),
        "Legacy schema v" + version + " did not emit a warning."
      );
    }
  }

  private static void TestUnsupportedDatabasesArePreserved(string root)
  {
    string unsupported = Path.Combine(root, "unsupported-v16.sqlite");
    CreateLegacyV15Database(unsupported, 16);
    AssertThrows<InvalidOperationException>(
      () =>
        LegacyActivityDatabaseTransition.EnsureCurrent(
          unsupported,
          unsupported + ".migrating",
          unsupported + ".backup"
        ),
      "Unsupported v16 database was silently initialized."
    );
    Assert(ReadUserVersion(unsupported) == 16, "Unsupported database was modified.");
    Assert(CountRows(unsupported, "runs") == 1, "Unsupported database lost activity data.");

    string corrupt = Path.Combine(root, "corrupt.sqlite");
    byte[] corruptBytes = { 1, 3, 3, 7, 9 };
    File.WriteAllBytes(corrupt, corruptBytes);
    AssertThrows<SqliteException>(
      () => LegacyActivityDatabaseTransition.EnsureCurrent(corrupt, corrupt + ".migrating", corrupt + ".backup"),
      "Corrupt database was silently initialized."
    );
    Assert(File.ReadAllBytes(corrupt).SequenceEqual(corruptBytes), "Corrupt source database was overwritten.");
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
      JudgmentSystem = RunJudgmentSystem.ModernCompetitive,
      JudgmentCounts = new JudgmentCounts
      {
        PerfectMinus = 1,
        XPerfect = 2,
        PerfectPlus = 3,
      },
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
