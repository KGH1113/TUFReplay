using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Shared.Database;

public static class MicrophoneDatabase
{
  private const int BusyTimeoutSeconds = 5;
  private const int AutoVacuumFull = 1;

  // SQLite keeps the -wal file at its largest size. A long recording's save or delete
  // passes through it, so trim it back after the next write.
  private const long WalSizeLimitBytes = 8L * 1024 * 1024;
  public const int SchemaVersion = 1;

  public static string DbPath { get; private set; }

  public static void Initialize(string path)
  {
    if (string.IsNullOrWhiteSpace(path))
      throw new ArgumentException("A microphone database path is required.", nameof(path));
    DbPath = path;
    using SqliteConnection connection = OpenConnection();
    EnsureSchema(connection);
  }

  public static SqliteConnection OpenConnection()
  {
    if (string.IsNullOrWhiteSpace(DbPath))
      throw new InvalidOperationException("The microphone database is not initialized.");
    var connection = new SqliteConnection("Data Source=" + DbPath + ";Default Timeout=" + BusyTimeoutSeconds);
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      "PRAGMA busy_timeout=" + (BusyTimeoutSeconds * 1000) + "; PRAGMA journal_size_limit=" + WalSizeLimitBytes + ";";
    command.ExecuteNonQuery();
    return connection;
  }

  // Deleted pages stay in a SQLite file, so without auto-vacuum the file keeps the size
  // of the most audio it ever held. With it, each delete shrinks the file by what it
  // removed. A file created before this setting is rebuilt once here. VACUUM rewrites
  // every remaining recording, so call this while gameplay is idle.
  public static bool EnableAutoVacuum()
  {
    using SqliteConnection connection = OpenConnection();
    using (SqliteCommand mode = connection.CreateCommand())
    {
      mode.CommandText = "PRAGMA auto_vacuum";
      if (Convert.ToInt32(mode.ExecuteScalar()) == AutoVacuumFull)
        return false;
    }
    using SqliteCommand rebuild = connection.CreateCommand();
    rebuild.CommandText = "PRAGMA auto_vacuum=FULL; VACUUM;";
    rebuild.ExecuteNonQuery();
    return true;
  }

  private static void EnsureSchema(SqliteConnection connection)
  {
    using SqliteCommand version = connection.CreateCommand();
    version.CommandText = "PRAGMA user_version";
    int current = Convert.ToInt32(version.ExecuteScalar());
    if (current > SchemaVersion)
      throw new InvalidOperationException(
        "TUFReplay microphone database schema is newer than this mod supports. version=" + current
      );
    // auto_vacuum only applies to a file with no tables yet, and must come before
    // WAL mode, which writes the first page. Older files go through EnableAutoVacuum.
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"PRAGMA auto_vacuum=FULL;
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
CREATE TABLE IF NOT EXISTS microphone_recordings (
  run_id TEXT PRIMARY KEY,
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
CREATE INDEX IF NOT EXISTS microphone_recordings_expiry ON microphone_recordings(is_permanent,expires_at_utc);
CREATE TABLE IF NOT EXISTS microphone_legacy_imports (
  source_path TEXT PRIMARY KEY,
  completed_at_utc TEXT NOT NULL
);
PRAGMA user_version = 1;";
    command.ExecuteNonQuery();
    EnsureTimelineColumn(connection);
  }

  // Added in place so the schema stays at version 1 and older builds, which name
  // their columns explicitly, can still read and write the file.
  private static void EnsureTimelineColumn(SqliteConnection connection)
  {
    using (SqliteCommand columns = connection.CreateCommand())
    {
      columns.CommandText = "PRAGMA table_info(microphone_recordings);";
      using SqliteDataReader reader = columns.ExecuteReader();
      while (reader.Read())
        if (string.Equals(reader.GetString(1), "timeline_json", StringComparison.Ordinal))
          return;
    }
    using SqliteCommand alter = connection.CreateCommand();
    alter.CommandText = "ALTER TABLE microphone_recordings ADD COLUMN timeline_json TEXT";
    alter.ExecuteNonQuery();
  }
}
