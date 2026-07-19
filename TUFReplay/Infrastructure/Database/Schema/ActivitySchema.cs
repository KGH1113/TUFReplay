using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class ActivitySchema
{
  public const int Version = 9;

  public static void Ensure(SqliteConnection connection)
  {
    int version;
    using (SqliteCommand versionCommand = connection.CreateCommand())
    {
      versionCommand.CommandText = "PRAGMA user_version;";
      version = System.Convert.ToInt32(versionCommand.ExecuteScalar());
    }

    if (version > Version)
    {
      throw new InvalidOperationException(
        "TUFReplay database schema is newer than this mod supports. version=" + version
      );
    }

    if (version == 2)
    {
      Migrate(connection, "ALTER TABLE runs ADD COLUMN x_accuracy REAL; PRAGMA user_version = 3;");
      version = 3;
    }

    if (version == 3)
    {
      Migrate(
        connection,
        @"
ALTER TABLE runs ADD COLUMN judgment_difficulty INTEGER;
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
        @"
ALTER TABLE runs ADD COLUMN gameplay_hash BLOB;
ALTER TABLE runs ADD COLUMN gameplay_hash_version INTEGER;
PRAGMA user_version = 5;"
      );
      version = 5;
    }

    if (version == 5)
    {
      Migrate(
        connection,
        @"
ALTER TABLE level_sessions ADD COLUMN song TEXT;
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
        @"
CREATE TABLE microphone_recordings (
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

    if (version != 0 && version != Version)
    {
      throw new InvalidOperationException("Unsupported TUFReplay database schema. version=" + version);
    }

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"
CREATE TABLE IF NOT EXISTS app_sessions (
  id TEXT PRIMARY KEY,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  recorder_time_zone_id TEXT,
  recorder_utc_offset_minutes INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS level_sessions (
  id TEXT PRIMARY KEY,
  app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  tuf_level_id INTEGER,
  level_path TEXT NOT NULL,
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT,
  level_tile_count INTEGER NOT NULL DEFAULT 0,
  level_file_hash BLOB,
  song TEXT,
  author TEXT,
  artist TEXT,
  metadata_state INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS runs (
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
  gameplay_hash BLOB,
  gameplay_hash_version INTEGER,
  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',
  meta_json TEXT NOT NULL DEFAULT '{}',
  UNIQUE(level_session_id, run_index)
);
CREATE TABLE IF NOT EXISTS microphone_recordings (
  run_id TEXT PRIMARY KEY REFERENCES runs(id) ON DELETE CASCADE,
  audio_wav BLOB NOT NULL,
  format TEXT NOT NULL,
  sample_rate INTEGER NOT NULL,
  channels INTEGER NOT NULL,
  frame_count INTEGER NOT NULL,
  device_id TEXT,
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_app_sessions_page ON app_sessions(started_at_utc DESC, id);
CREATE INDEX IF NOT EXISTS idx_level_sessions_app ON level_sessions(app_session_id, opened_at_utc, id);
CREATE INDEX IF NOT EXISTS idx_runs_level_index ON runs(level_session_id, run_index);
CREATE INDEX IF NOT EXISTS idx_runs_start_tile ON runs(level_session_id, start_tile, run_index);
PRAGMA user_version = 9;";
    command.ExecuteNonQuery();
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
