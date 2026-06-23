using System.IO;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Shared;

public static class Database
{
  public static string DbPath { get; private set; }

  public static void Initialize()
  {
    NativeSqliteLoader.Initialize();

    string dir = Path.Combine(Main.Instance.Path, "Data");
    Directory.CreateDirectory(dir);
    DbPath = Path.Combine(dir, "tufreplay.sqlite");

    using SqliteConnection connection = OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
CREATE TABLE IF NOT EXISTS play_records (
  id TEXT PRIMARY KEY,
  tuf_level_id INTEGER NOT NULL,
  cleared_at_utc TEXT NOT NULL,
  started_at_utc TEXT NOT NULL,
  ended_at_utc TEXT,
  input_count INTEGER NOT NULL,
  submitted INTEGER NOT NULL DEFAULT 0,
  meta_json TEXT NOT NULL,
  input_csv BLOB NOT NULL,
  mic_record BLOB
);

CREATE INDEX IF NOT EXISTS idx_play_records_cleared_at
ON play_records(cleared_at_utc);
";

    command.ExecuteNonQuery();
  }

  public static SqliteConnection OpenConnection()
  {
    SqliteConnection connection = new SqliteConnection("Data Source=" + DbPath);
    connection.Open();
    return connection;
  }
}
