using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class ActivitySchema
{
  public const int Version = 3;

  public static void Ensure(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA user_version;";
    int version = System.Convert.ToInt32(command.ExecuteScalar());

    if (version > Version)
    {
      throw new InvalidOperationException("TUFReplay database schema is newer than this mod supports. version=" + version);
    }

    if (version == 2)
    {
      using SqliteTransaction transaction = connection.BeginTransaction();
      command.Transaction = transaction;
      command.CommandText = "ALTER TABLE runs ADD COLUMN x_accuracy REAL; PRAGMA user_version = 3;";
      command.ExecuteNonQuery();
      transaction.Commit();
      return;
    }

    if (version != 0 && version != Version)
    {
      throw new InvalidOperationException("Unsupported TUFReplay database schema. version=" + version);
    }

    command.CommandText = @"
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
  level_tile_count INTEGER NOT NULL DEFAULT 0
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
  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',
  meta_json TEXT NOT NULL DEFAULT '{}',
  UNIQUE(level_session_id, run_index)
);
CREATE INDEX IF NOT EXISTS idx_app_sessions_page ON app_sessions(started_at_utc DESC, id);
CREATE INDEX IF NOT EXISTS idx_level_sessions_app ON level_sessions(app_session_id, opened_at_utc, id);
CREATE INDEX IF NOT EXISTS idx_runs_level_index ON runs(level_session_id, run_index);
CREATE INDEX IF NOT EXISTS idx_runs_start_tile ON runs(level_session_id, start_tile, run_index);
PRAGMA user_version = 3;";
    command.ExecuteNonQuery();
  }
}
