using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Infrastructure.Database.Schema;

public static class ActivitySchema
{
  public const int Version = 11;

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

    if (version == 9)
    {
      Migrate(
        connection,
        @"
ALTER TABLE microphone_recordings ADD COLUMN is_permanent INTEGER NOT NULL DEFAULT 1;
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
  logical_level_id TEXT NOT NULL REFERENCES logical_levels(id),
  app_session_id TEXT NOT NULL REFERENCES app_sessions(id),
  tuf_level_id INTEGER,
  level_path TEXT NOT NULL,
  opened_at_utc TEXT NOT NULL,
  closed_at_utc TEXT,
  level_tile_count INTEGER NOT NULL DEFAULT 0,
  level_file_hash BLOB,
  gameplay_hash BLOB,
  gameplay_hash_version INTEGER,
  song TEXT,
  author TEXT,
  artist TEXT,
  metadata_state INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS logical_levels (
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
  capture_start_offset_us INTEGER NOT NULL DEFAULT 0,
  is_permanent INTEGER NOT NULL DEFAULT 0,
  expires_at_utc TEXT
);
CREATE INDEX IF NOT EXISTS idx_app_sessions_page ON app_sessions(started_at_utc DESC, id);
CREATE INDEX IF NOT EXISTS idx_level_sessions_app ON level_sessions(app_session_id, opened_at_utc, id);
CREATE INDEX IF NOT EXISTS idx_level_sessions_logical ON level_sessions(logical_level_id, opened_at_utc, id);
CREATE INDEX IF NOT EXISTS idx_runs_level_index ON runs(level_session_id, run_index);
CREATE INDEX IF NOT EXISTS idx_runs_start_tile ON runs(level_session_id, start_tile, run_index);
PRAGMA user_version = 11;";
    command.ExecuteNonQuery();
  }

  private static void MigrateLogicalLevels(SqliteConnection connection)
  {
    using SqliteTransaction transaction = connection.BeginTransaction();
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"
CREATE TABLE logical_levels (
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
";
      command.ExecuteNonQuery();
    }

    var sessions = new List<LevelSession>();
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"SELECT id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
level_file_hash,gameplay_hash,gameplay_hash_version,song,author,artist,metadata_state FROM level_sessions";
      using SqliteDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
        sessions.Add(
          new LevelSession
          {
            Id = reader.GetString(0),
            AppSessionId = reader.GetString(1),
            TufLevelId = DbValue.NullableInt(reader, 2),
            LevelPath = reader.GetString(3),
            OpenedAtUtc = reader.GetString(4),
            ClosedAtUtc = DbValue.NullableString(reader, 5),
            LevelTileCount = reader.GetInt32(6),
            LevelFileHash = reader.IsDBNull(7) ? null : (byte[])reader.GetValue(7),
            GameplayHash = reader.IsDBNull(8) ? null : (byte[])reader.GetValue(8),
            GameplayHashVersion = DbValue.NullableInt(reader, 9),
            Song = DbValue.NullableString(reader, 10),
            Author = DbValue.NullableString(reader, 11),
            Artist = DbValue.NullableString(reader, 12),
            MetadataState = (LevelMetadataState)reader.GetInt32(13),
          }
        );
      }
    }

    foreach (LevelSession session in sessions)
    {
      string logicalLevelId = LogicalLevelRepository.ResolveOrCreate(connection, transaction, session);
      using SqliteCommand update = connection.CreateCommand();
      update.Transaction = transaction;
      update.CommandText = "UPDATE level_sessions SET logical_level_id=@logical WHERE id=@id";
      update.Parameters.AddWithValue("@logical", logicalLevelId);
      update.Parameters.AddWithValue("@id", session.Id);
      update.ExecuteNonQuery();
    }

    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"CREATE INDEX idx_level_sessions_logical ON level_sessions(logical_level_id,opened_at_utc,id);
PRAGMA user_version = 11;";
      command.ExecuteNonQuery();
    }
    transaction.Commit();
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
