using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Migrations;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Database;

namespace TUFReplay.Shared.Database;

public static class Database
{
  private const int BusyTimeoutSeconds = 5;
  private const int LockRetryCount = 3;

  public static string DbPath { get; private set; }

  public static void Initialize()
  {
    NativeSqliteLoader.Initialize();

    string dir = Path.Combine(Main.Instance.InstallPath, "Data");
    Directory.CreateDirectory(dir);
    DbPath = Path.Combine(dir, "tufreplay.sqlite");
    string migratingPath = Path.Combine(dir, "tufreplay.0.2.migrating.sqlite");
    string backupPath = Path.Combine(dir, "tufreplay.pre-0.2.sqlite");
    string microphonePath = Path.Combine(dir, "tufreplay.microphones.sqlite");

    MicrophoneDatabase.Initialize(microphonePath);
    LegacyActivityDatabaseTransition.EnsureCurrent(
      DbPath,
      migratingPath,
      backupPath,
      message => Main.Instance?.Log(message)
    );
    using (SqliteConnection connection = OpenConnection())
      DatabaseSchema.Ensure(connection);
    MicrophoneRecordingRepository.ImportEmbeddedLegacyRecordings(backupPath);
    MicrophoneRecordingRepository.DeleteOrphans();
  }

  public static SqliteConnection OpenConnection()
  {
    SqliteConnection connection = new SqliteConnection(
      "Data Source=" + DbPath + ";Default Timeout=" + BusyTimeoutSeconds
    );
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=" + (BusyTimeoutSeconds * 1000) + ";";
    command.ExecuteNonQuery();
    return connection;
  }

  public static int ExecuteNonQueryWithLockRetry(SqliteCommand command)
  {
    for (int attempt = 1; ; attempt++)
    {
      try
      {
        return command.ExecuteNonQuery();
      }
      catch (SqliteException exception) when (IsTransientLock(exception) && attempt < LockRetryCount)
      {
        Thread.Sleep(attempt * 100);
      }
    }
  }

  public static bool IsTransientLock(SqliteException exception)
  {
    return exception != null && (exception.SqliteErrorCode == 5 || exception.SqliteErrorCode == 6);
  }
}
