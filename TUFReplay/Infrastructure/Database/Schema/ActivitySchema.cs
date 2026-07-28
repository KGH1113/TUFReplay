using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class ActivitySchema
{
  public const int Version = 14;

  public static void Ensure(SqliteConnection connection)
  {
    int version;
    using (SqliteCommand versionCommand = connection.CreateCommand())
    {
      versionCommand.CommandText = "PRAGMA user_version;";
      version = Convert.ToInt32(versionCommand.ExecuteScalar());
    }

    if (version > Version)
      throw new InvalidOperationException(
        "TUFReplay database schema is newer than this mod supports. version=" + version
      );

    if (version == 2)
    {
      Migrate(connection, "ALTER TABLE runs ADD COLUMN x_accuracy REAL; PRAGMA user_version = 3;");
      version = 3;
    }
    if (version == 3)
    {
      Migrate(
        connection,
        @"ALTER TABLE runs ADD COLUMN judgment_difficulty INTEGER;
ALTER TABLE runs ADD COLUMN judgment_overload INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_too_early INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_early INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_early_perfect INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_perfect INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_late_perfect INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_late INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_too_late INTEGER NOT NULL DEFAULT 0;
ALTER TABLE runs ADD COLUMN judgment_miss INTEGER NOT NULL DEFAULT 0;
PRAGMA user_version = 4;"
      );
      version = 4;
    }
    if (version == 4)
    {
      Migrate(
        connection,
        @"ALTER TABLE runs ADD COLUMN gameplay_hash BLOB;
ALTER TABLE runs ADD COLUMN gameplay_hash_version INTEGER;
PRAGMA user_version = 5;"
      );
      version = 5;
    }
    if (version == 5)
    {
      Migrate(
        connection,
        @"ALTER TABLE level_sessions ADD COLUMN song TEXT;
ALTER TABLE level_sessions ADD COLUMN author TEXT;
ALTER TABLE level_sessions ADD COLUMN artist TEXT;
ALTER TABLE level_sessions ADD COLUMN metadata_state INTEGER NOT NULL DEFAULT 0;
PRAGMA user_version = 6;"
      );
      version = 6;
    }
    if (version == 6)
    {
      SkyHookInputKeyMigration.Migrate(connection, out _, out _, out _);
      version = 7;
    }
    if (version == 7)
    {
      Migrate(
        connection,
        @"CREATE TABLE microphone_recordings (
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,
  audio_wav BLOB NOT NULL,
  format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,
  channels INTEGER NOT NULL,
  frame_count INTEGER NOT NULL,
  device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0
);
PRAGMA user_version = 8;"
      );
      version = 8;
    }
    if (version == 8)
    {
      Migrate(connection, "ALTER TABLE level_sessions ADD COLUMN level_file_hash BLOB; PRAGMA user_version = 9;");
      version = 9;
    }
    if (version == 9)
    {
      Migrate(
        connection,
        @"ALTER TABLE microphone_recordings ADD COLUMN is_permanent INTEGER NOT NULL DEFAULT 1;
ALTER TABLE microphone_recordings ADD COLUMN expires_at_utc TEXT;
PRAGMA user_version = 10;"
      );
      version = 10;
    }
    if (version == 10)
    {
      MigrateLogicalLevels(connection);
      version = 11;
    }
    if (version == 11)
    {
      Migrate(
        connection,
        @"CREATE TABLE gameplay_snapshots (
  gameplay_hash BLOB NOT NULL,
  gameplay_hash_version INTEGER NOT NULL,
  chart_json_utf8 BLOB NOT NULL,
  created_at_utc TEXT NOT NULL,
  PRIMARY KEY(gameplay_hash, gameplay_hash_version)
);
PRAGMA user_version = 12;"
      );
      version = 12;
    }
    if (version == 12)
    {
      MigrateLevels(connection);
      version = 13;
    }
    if (version == 13)
    {
      RepairRenamedForeignKeys(connection);
      version = 14;
    }

    if (version != 0 && version != Version)
      throw new InvalidOperationException("Unsupported TUFReplay database schema. version=" + version);

    if (version == 0)
      CreateCurrent(connection);
  }

  private static void CreateCurrent(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"CREATE TABLE app_sessions (
  id TEXT PRIMARY KEY,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  recorder_time_zone_id TEXT,
  recorder_utc_offset_minutes INTEGER NOT NULL
);
CREATE TABLE levels (
  id TEXT PRIMARY KEY,
  identity_key TEXT NOT NULL UNIQUE,
  source_kind INTEGER NOT NULL,
  tuf_level_id INTEGER,
  adofai_path TEXT NOT NULL,
  gameplay_hash BLOB,
  gameplay_hash_version INTEGER,
  level_tile_count INTEGER NOT NULL DEFAULT 0,
  song TEXT,
  author TEXT,
  artist TEXT,
  metadata_state INTEGER NOT NULL DEFAULT 0,
  first_seen_at_utc TEXT NOT NULL,
  last_seen_at_utc TEXT NOT NULL,
  CHECK(source_kind IN (0,1)),
  CHECK((source_kind=0 AND tuf_level_id IS NULL) OR (source_kind=1 AND tuf_level_id IS NOT NULL))
);
CREATE TABLE level_sessions (
  id TEXT PRIMARY KEY,
  level_id TEXT NOT NULL REFERENCES levels(id),
  app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT
);
CREATE TABLE runs (
  id TEXT PRIMARY KEY,
  level_session_id TEXT NOT NULL REFERENCES level_sessions(id),
  run_index INTEGER NOT NULL,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  start_tile INTEGER NOT NULL DEFAULT 0,
  last_tile INTEGER,
  result TEXT NOT NULL DEFAULT 'unknown',
  no_fail_mode INTEGER NOT NULL DEFAULT 0,
  gameplay_start_song_position REAL,
  level_pitch_percent INTEGER,
  effective_pitch REAL,
  x_accuracy REAL,
  judgment_difficulty INTEGER,
  judgment_overload INTEGER NOT NULL DEFAULT 0,
  judgment_too_early INTEGER NOT NULL DEFAULT 0,
  judgment_early INTEGER NOT NULL DEFAULT 0,
  judgment_early_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late INTEGER NOT NULL DEFAULT 0,
  judgment_too_late INTEGER NOT NULL DEFAULT 0,
  judgment_miss INTEGER NOT NULL DEFAULT 0,
  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',
  meta_json TEXT NOT NULL DEFAULT '{}',
  UNIQUE(level_session_id, run_index)
);
CREATE TABLE microphone_recordings (
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,
  audio_wav BLOB NOT NULL,
  format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,
  channels INTEGER NOT NULL,
  frame_count INTEGER NOT NULL,
  device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0,
  is_permanent INTEGER NOT NULL DEFAULT 0,
  expires_at_utc TEXT
);
CREATE INDEX idx_app_sessions_page ON app_sessions(started_at_utc DESC,id);
CREATE INDEX idx_level_sessions_app ON level_sessions(app_session_id,opened_at_utc,id);
CREATE INDEX idx_level_sessions_level ON level_sessions(level_id,opened_at_utc,id);
CREATE INDEX idx_runs_level_index ON runs(level_session_id,run_index);
CREATE INDEX idx_runs_start_tile ON runs(level_session_id,start_tile,run_index);
PRAGMA user_version = 14;";
    command.ExecuteNonQuery();
  }

  private static void MigrateLogicalLevels(SqliteConnection connection)
  {
    Migrate(
      connection,
      @"CREATE TABLE logical_levels (
  id TEXT PRIMARY KEY,
  identity_key TEXT NOT NULL UNIQUE,
  tuf_level_id INTEGER,
  gameplay_hash BLOB,
  gameplay_hash_version INTEGER,
  song TEXT,
  author TEXT,
  artist TEXT,
  first_seen_at_utc TEXT NOT NULL,
  last_seen_at_utc TEXT NOT NULL
);
ALTER TABLE level_sessions ADD COLUMN logical_level_id TEXT REFERENCES logical_levels(id);
ALTER TABLE level_sessions ADD COLUMN gameplay_hash BLOB;
ALTER TABLE level_sessions ADD COLUMN gameplay_hash_version INTEGER;
UPDATE level_sessions SET
  gameplay_hash=(SELECT gameplay_hash FROM runs WHERE level_session_id=level_sessions.id AND gameplay_hash IS NOT NULL ORDER BY run_index LIMIT 1),
  gameplay_hash_version=(SELECT gameplay_hash_version FROM runs WHERE level_session_id=level_sessions.id AND gameplay_hash IS NOT NULL ORDER BY run_index LIMIT 1);
INSERT INTO logical_levels(
  id,identity_key,tuf_level_id,gameplay_hash,gameplay_hash_version,song,author,artist,first_seen_at_utc,last_seen_at_utc
)
SELECT id,'legacy:'||id,tuf_level_id,gameplay_hash,gameplay_hash_version,song,author,artist,
       opened_at_utc,coalesce(closed_at_utc,opened_at_utc)
FROM level_sessions;
UPDATE level_sessions SET logical_level_id=id;
CREATE INDEX idx_level_sessions_logical ON level_sessions(logical_level_id,opened_at_utc,id);
PRAGMA user_version = 11;"
    );
  }

  private static void MigrateLevels(SqliteConnection connection)
  {
    using (SqliteCommand foreignKeys = connection.CreateCommand())
    {
      foreignKeys.CommandText = "PRAGMA foreign_keys=OFF;";
      foreignKeys.ExecuteNonQuery();
    }

    using SqliteTransaction transaction = connection.BeginTransaction();
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"CREATE TABLE levels_new (
  id TEXT PRIMARY KEY,
  identity_key TEXT NOT NULL UNIQUE,
  source_kind INTEGER NOT NULL,
  tuf_level_id INTEGER,
  adofai_path TEXT NOT NULL,
  gameplay_hash BLOB,
  gameplay_hash_version INTEGER,
  level_tile_count INTEGER NOT NULL DEFAULT 0,
  song TEXT,
  author TEXT,
  artist TEXT,
  metadata_state INTEGER NOT NULL DEFAULT 0,
  first_seen_at_utc TEXT NOT NULL,
  last_seen_at_utc TEXT NOT NULL,
  CHECK(source_kind IN (0,1)),
  CHECK((source_kind=0 AND tuf_level_id IS NULL) OR (source_kind=1 AND tuf_level_id IS NOT NULL))
);
CREATE TEMP TABLE level_migration_map(session_id TEXT PRIMARY KEY,identity_key TEXT NOT NULL);
INSERT INTO level_migration_map(session_id,identity_key)
SELECT id,
  CASE WHEN tuf_level_id IS NULL THEN 'local:'||length(level_path)||':'||level_path||':'
       ELSE 'tuf:'||tuf_level_id||':' END
  ||CASE WHEN gameplay_hash IS NULL THEN 'unknown:'||id
        ELSE coalesce(gameplay_hash_version,-1)||':'||hex(gameplay_hash) END
FROM level_sessions;
INSERT INTO levels_new(
  id,identity_key,source_kind,tuf_level_id,adofai_path,gameplay_hash,gameplay_hash_version,
  level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
)
SELECT lower(hex(randomblob(16))),m.identity_key,CASE WHEN s.tuf_level_id IS NULL THEN 0 ELSE 1 END,
       s.tuf_level_id,s.level_path,s.gameplay_hash,s.gameplay_hash_version,s.level_tile_count,
       s.song,s.author,s.artist,s.metadata_state,min(s.opened_at_utc),max(coalesce(s.closed_at_utc,s.opened_at_utc))
FROM level_sessions s JOIN level_migration_map m ON m.session_id=s.id
GROUP BY m.identity_key;
CREATE TABLE level_sessions_new (
  id TEXT PRIMARY KEY,
  level_id TEXT NOT NULL REFERENCES levels_new(id),
  app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT
);
INSERT INTO level_sessions_new(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
SELECT s.id,l.id,s.app_session_id,s.opened_at_utc,s.closed_at_utc
FROM level_sessions s
JOIN level_migration_map m ON m.session_id=s.id
JOIN levels_new l ON l.identity_key=m.identity_key;
CREATE TABLE runs_new (
  id TEXT PRIMARY KEY,
  level_session_id TEXT NOT NULL REFERENCES level_sessions_new(id),
  run_index INTEGER NOT NULL,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  start_tile INTEGER NOT NULL DEFAULT 0,
  last_tile INTEGER,
  result TEXT NOT NULL DEFAULT 'unknown',
  no_fail_mode INTEGER NOT NULL DEFAULT 0,
  gameplay_start_song_position REAL,
  level_pitch_percent INTEGER,
  effective_pitch REAL,
  x_accuracy REAL,
  judgment_difficulty INTEGER,
  judgment_overload INTEGER NOT NULL DEFAULT 0,
  judgment_too_early INTEGER NOT NULL DEFAULT 0,
  judgment_early INTEGER NOT NULL DEFAULT 0,
  judgment_early_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late INTEGER NOT NULL DEFAULT 0,
  judgment_too_late INTEGER NOT NULL DEFAULT 0,
  judgment_miss INTEGER NOT NULL DEFAULT 0,
  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',
  meta_json TEXT NOT NULL DEFAULT '{}',
  UNIQUE(level_session_id,run_index)
);
INSERT INTO runs_new(
  id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
  judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,
  input_count,hit_context_count,input_csv,hit_context_csv,meta_json
)
SELECT id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
  judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,
  input_count,hit_context_count,input_csv,hit_context_csv,meta_json
FROM runs;
DROP INDEX IF EXISTS idx_level_sessions_logical;
DROP INDEX IF EXISTS idx_level_sessions_app;
DROP INDEX IF EXISTS idx_runs_level_index;
DROP INDEX IF EXISTS idx_runs_start_tile;
DROP TABLE runs;
DROP TABLE level_sessions;
DROP TABLE logical_levels;
DROP TABLE gameplay_snapshots;
ALTER TABLE levels_new RENAME TO levels;
ALTER TABLE level_sessions_new RENAME TO level_sessions;
ALTER TABLE runs_new RENAME TO runs;
CREATE INDEX idx_level_sessions_app ON level_sessions(app_session_id,opened_at_utc,id);
CREATE INDEX idx_level_sessions_level ON level_sessions(level_id,opened_at_utc,id);
CREATE INDEX idx_runs_level_index ON runs(level_session_id,run_index);
CREATE INDEX idx_runs_start_tile ON runs(level_session_id,start_tile,run_index);
PRAGMA user_version = 13;";
      command.ExecuteNonQuery();
    }
    transaction.Commit();

    using SqliteCommand foreignKeyCheck = connection.CreateCommand();
    foreignKeyCheck.CommandText = "PRAGMA foreign_keys=ON; PRAGMA foreign_key_check;";
    using SqliteDataReader violations = foreignKeyCheck.ExecuteReader();
    if (violations.Read())
      throw new InvalidOperationException("Database migration created a foreign-key violation.");
  }

  private static void RepairRenamedForeignKeys(SqliteConnection connection)
  {
    if (
      ForeignKeyTargetsTable(connection, "level_sessions", "level_id", "levels")
      && ForeignKeyTargetsTable(connection, "runs", "level_session_id", "level_sessions")
    )
    {
      Migrate(connection, "PRAGMA user_version = 14;");
      return;
    }

    SetForeignKeys(connection, enabled: false);
    try
    {
      using SqliteTransaction transaction = connection.BeginTransaction();
      using (SqliteCommand command = connection.CreateCommand())
      {
        command.Transaction = transaction;
        command.CommandText =
          @"CREATE TEMP TABLE level_sessions_v14_backup AS SELECT * FROM level_sessions;
CREATE TEMP TABLE runs_v14_backup AS SELECT * FROM runs;
DROP INDEX IF EXISTS idx_level_sessions_app;
DROP INDEX IF EXISTS idx_level_sessions_level;
DROP INDEX IF EXISTS idx_runs_level_index;
DROP INDEX IF EXISTS idx_runs_start_tile;
DROP TABLE runs;
DROP TABLE level_sessions;
CREATE TABLE level_sessions (
  id TEXT PRIMARY KEY,
  level_id TEXT NOT NULL REFERENCES levels(id),
  app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT
);
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
SELECT id,level_id,app_session_id,opened_at_utc,closed_at_utc FROM level_sessions_v14_backup;
CREATE TABLE runs (
  id TEXT PRIMARY KEY,
  level_session_id TEXT NOT NULL REFERENCES level_sessions(id),
  run_index INTEGER NOT NULL,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  start_tile INTEGER NOT NULL DEFAULT 0,
  last_tile INTEGER,
  result TEXT NOT NULL DEFAULT 'unknown',
  no_fail_mode INTEGER NOT NULL DEFAULT 0,
  gameplay_start_song_position REAL,
  level_pitch_percent INTEGER,
  effective_pitch REAL,
  x_accuracy REAL,
  judgment_difficulty INTEGER,
  judgment_overload INTEGER NOT NULL DEFAULT 0,
  judgment_too_early INTEGER NOT NULL DEFAULT 0,
  judgment_early INTEGER NOT NULL DEFAULT 0,
  judgment_early_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late_perfect INTEGER NOT NULL DEFAULT 0,
  judgment_late INTEGER NOT NULL DEFAULT 0,
  judgment_too_late INTEGER NOT NULL DEFAULT 0,
  judgment_miss INTEGER NOT NULL DEFAULT 0,
  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',
  meta_json TEXT NOT NULL DEFAULT '{}',
  UNIQUE(level_session_id,run_index)
);
INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
  judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,
  input_count,hit_context_count,input_csv,hit_context_csv,meta_json
)
SELECT id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
  judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,
  input_count,hit_context_count,input_csv,hit_context_csv,meta_json
FROM runs_v14_backup;
DROP TABLE level_sessions_v14_backup;
DROP TABLE runs_v14_backup;
CREATE INDEX idx_level_sessions_app ON level_sessions(app_session_id,opened_at_utc,id);
CREATE INDEX idx_level_sessions_level ON level_sessions(level_id,opened_at_utc,id);
CREATE INDEX idx_runs_level_index ON runs(level_session_id,run_index);
CREATE INDEX idx_runs_start_tile ON runs(level_session_id,start_tile,run_index);
PRAGMA user_version = 14;";
        command.ExecuteNonQuery();
      }

      EnsureNoForeignKeyViolations(connection, transaction);
      transaction.Commit();
    }
    finally
    {
      SetForeignKeys(connection, enabled: true);
    }
  }

  private static bool ForeignKeyTargetsTable(
    SqliteConnection connection,
    string table,
    string column,
    string targetTable
  )
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA foreign_key_list(" + table + ");";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      if (
        string.Equals(reader.GetString(3), column, StringComparison.OrdinalIgnoreCase)
        && string.Equals(reader.GetString(2), targetTable, StringComparison.OrdinalIgnoreCase)
      )
      {
        return true;
      }
    }

    return false;
  }

  private static void SetForeignKeys(SqliteConnection connection, bool enabled)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = enabled ? "PRAGMA foreign_keys=ON;" : "PRAGMA foreign_keys=OFF;";
    command.ExecuteNonQuery();
  }

  private static void EnsureNoForeignKeyViolations(SqliteConnection connection, SqliteTransaction transaction)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "PRAGMA foreign_key_check;";
    using SqliteDataReader violations = command.ExecuteReader();
    if (violations.Read())
      throw new InvalidOperationException("Database migration created a foreign-key violation.");
  }

  private static void Migrate(SqliteConnection connection, string sql)
  {
    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = sql;
    command.ExecuteNonQuery();
    transaction.Commit();
  }
}
