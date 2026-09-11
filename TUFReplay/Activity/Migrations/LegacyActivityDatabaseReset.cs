using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static class LegacyActivityDatabaseReset
{
  public const int MaximumDiscardedVersion = 15;

  public static void EnsureCurrent(string path, Action<string> logWarning = null)
  {
    if (!File.Exists(path))
    {
      CreateCurrent(path);
      return;
    }

    int applicationId;
    int version;
    using (SqliteConnection connection = Open(path))
    {
      ActivitySchema.ReadHeader(connection, out applicationId, out version);
      if (applicationId == ActivitySchema.ApplicationId && version == ActivitySchema.Version)
      {
        ActivitySchema.Validate(connection);
        return;
      }

      if (applicationId != 0 || version < 0 || version > MaximumDiscardedVersion)
        throw new InvalidOperationException(
          "Unsupported TUFReplay activity database. applicationId=" + applicationId + ", version=" + version
        );

      using SqliteCommand checkpoint = connection.CreateCommand();
      checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
      checkpoint.ExecuteNonQuery();
    }

    logWarning?.Invoke(
      "[Database] Legacy activity schema v"
        + version
        + " is no longer supported. Deleting the legacy activity database and starting fresh."
    );
    DeleteDatabase(path);
    CreateCurrent(path);
  }

  private static void CreateCurrent(string path)
  {
    using SqliteConnection connection = Open(path);
    ActivitySchema.Ensure(connection);
    ActivitySchema.Validate(connection);
  }

  private static SqliteConnection Open(string path)
  {
    var connection = new SqliteConnection("Data Source=" + path + ";Pooling=False");
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
    command.ExecuteNonQuery();
    return connection;
  }

  private static void DeleteDatabase(string path)
  {
    DeleteIfExists(path);
    DeleteIfExists(path + "-wal");
    DeleteIfExists(path + "-shm");
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }
}
