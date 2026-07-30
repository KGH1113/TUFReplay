using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Infrastructure.Database;

public static class MicrophoneDatabase
{
  private const int BusyTimeoutSeconds = 5;
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
    var connection = new SqliteConnection(
      "Data Source=" + DbPath + ";Default Timeout=" + BusyTimeoutSeconds
    );
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA busy_timeout=" + (BusyTimeoutSeconds * 1000) + ";";
    command.ExecuteNonQuery();
    return connection;
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
    if (current == SchemaVersion)
      return;

    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"PRAGMA journal_mode=WAL;
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
PRAGMA user_version = 1;";
    command.ExecuteNonQuery();
  }
}
