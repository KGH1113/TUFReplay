using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static partial class ActivitySchema
{
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
CREATE TABLE gameplay_hash_migration_attempts (
  level_id TEXT PRIMARY KEY REFERENCES levels(id) ON DELETE CASCADE,
  file_size INTEGER,
  file_modified_utc_ticks INTEGER,
  result TEXT NOT NULL,
  attempted_at_utc TEXT NOT NULL
);
CREATE INDEX idx_app_sessions_page ON app_sessions(started_at_utc DESC,id);
CREATE INDEX idx_level_sessions_app ON level_sessions(app_session_id,opened_at_utc,id);
CREATE INDEX idx_level_sessions_level ON level_sessions(level_id,opened_at_utc,id);
CREATE INDEX idx_runs_level_index ON runs(level_session_id,run_index);
CREATE INDEX idx_runs_start_tile ON runs(level_session_id,start_tile,run_index);
PRAGMA user_version = 15;";
    command.ExecuteNonQuery();
  }
}
