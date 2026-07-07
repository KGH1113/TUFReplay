using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class ActivitySchema
{
  public static void Ensure(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
CREATE TABLE IF NOT EXISTS app_sessions (
  id TEXT PRIMARY KEY,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT
);

CREATE TABLE IF NOT EXISTS level_sessions (
  id TEXT PRIMARY KEY,
  app_session_id TEXT NOT NULL,
  tuf_level_id INTEGER NOT NULL,
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT,
  level_tile_count INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS runs (
  id TEXT PRIMARY KEY,

  app_session_id TEXT NOT NULL,
  level_session_id TEXT NOT NULL,
  tuf_level_id INTEGER NOT NULL,

  run_index INTEGER NOT NULL,
  segment_group_index INTEGER NOT NULL DEFAULT 0,

  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,

  level_tile_count INTEGER NOT NULL DEFAULT 0,
  start_tile INTEGER NOT NULL DEFAULT 0,
  last_tile INTEGER,

  result TEXT NOT NULL DEFAULT 'unknown',
  no_fail_mode INTEGER NOT NULL DEFAULT 0,

  gameplay_start_song_position REAL,

  level_pitch_percent INTEGER,
  effective_pitch REAL,

  input_count INTEGER NOT NULL DEFAULT 0,
  hit_context_count INTEGER NOT NULL DEFAULT 0,
  input_csv BLOB NOT NULL DEFAULT X'',
  hit_context_csv BLOB NOT NULL DEFAULT X'',

  meta_json TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_app_sessions_started
ON app_sessions(started_at_utc);

CREATE INDEX IF NOT EXISTS idx_level_sessions_app
ON level_sessions(app_session_id, opened_at_utc);

CREATE INDEX IF NOT EXISTS idx_level_sessions_level
ON level_sessions(tuf_level_id, opened_at_utc);

CREATE INDEX IF NOT EXISTS idx_runs_level_session_index
ON runs(level_session_id, run_index);

CREATE INDEX IF NOT EXISTS idx_runs_chart
ON runs(level_session_id, started_at_utc, start_tile, last_tile);

CREATE INDEX IF NOT EXISTS idx_runs_start_group
ON runs(level_session_id, start_tile, started_at_utc);

CREATE INDEX IF NOT EXISTS idx_runs_segment_group
ON runs(level_session_id, segment_group_index, run_index);

CREATE INDEX IF NOT EXISTS idx_runs_result
ON runs(result);
";

    command.ExecuteNonQuery();
  }
}
