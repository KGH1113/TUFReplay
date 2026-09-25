using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static partial class ActivitySchema
{
  public const int ApplicationId = 0x54554652;
  public const int Version = 2;

  public static void Ensure(SqliteConnection connection)
  {
    ReadHeader(connection, out int applicationId, out int version);
    if (applicationId == 0 && version == 0)
    {
      CreateCurrent(connection);
      return;
    }

    if (applicationId == ApplicationId && version == 1)
    {
      UpgradeV1(connection);
      return;
    }

    if (applicationId != ApplicationId || version != Version)
      throw new InvalidOperationException(
        "Unsupported TUFReplay activity database. applicationId=" + applicationId + ", version=" + version
      );

    ValidateStructure(connection);
  }

  public static bool IsCurrent(SqliteConnection connection)
  {
    ReadHeader(connection, out int applicationId, out int version);
    return applicationId == ApplicationId && version == Version;
  }

  internal static void ReadHeader(SqliteConnection connection, out int applicationId, out int version)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA application_id;";
    applicationId = Convert.ToInt32(command.ExecuteScalar());
    command.CommandText = "PRAGMA user_version;";
    version = Convert.ToInt32(command.ExecuteScalar());
  }

  internal static void Validate(SqliteConnection connection)
  {
    ValidateStructure(connection);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA integrity_check;";
    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("TUFReplay activity database integrity check failed.");

    command.CommandText = "PRAGMA foreign_key_check;";
    using SqliteDataReader violations = command.ExecuteReader();
    if (violations.Read())
      throw new InvalidOperationException("TUFReplay activity database contains a foreign-key violation.");
  }

  private static void UpgradeV1(SqliteConnection connection)
  {
    foreach (string table in new[] { "app_sessions", "levels", "level_sessions", "runs", "replay_artifacts" })
      RequireTable(connection, table);

    using SqliteTransaction transaction = connection.BeginTransaction();
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    if (!HasColumn(connection, "runs", "submission_run_id", transaction))
    {
      command.CommandText = "ALTER TABLE runs ADD COLUMN submission_run_id TEXT";
      command.ExecuteNonQuery();
    }
    command.CommandText =
      "CREATE UNIQUE INDEX IF NOT EXISTS idx_runs_submission_run_id ON runs(submission_run_id) WHERE submission_run_id IS NOT NULL";
    command.ExecuteNonQuery();
    command.CommandText = "PRAGMA user_version = 2";
    command.ExecuteNonQuery();
    transaction.Commit();
    Validate(connection);
  }

  private static void ValidateStructure(SqliteConnection connection)
  {
    foreach (string table in new[] { "app_sessions", "levels", "level_sessions", "runs", "replay_artifacts" })
      RequireTable(connection, table);
    if (!HasColumn(connection, "runs", "submission_run_id"))
      throw new InvalidOperationException("TUFReplay activity database is missing runs.submission_run_id.");
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='index' AND name='idx_runs_submission_run_id'";
    if (command.ExecuteScalar() == null)
      throw new InvalidOperationException("TUFReplay activity database is missing idx_runs_submission_run_id.");
  }

  private static void RequireTable(SqliteConnection connection, string table)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name";
    command.Parameters.AddWithValue("@name", table);
    if (command.ExecuteScalar() == null)
      throw new InvalidOperationException("TUFReplay activity database is missing table " + table + ".");
  }

  private static bool HasColumn(
    SqliteConnection connection,
    string table,
    string column,
    SqliteTransaction transaction = null
  )
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "PRAGMA table_info(" + table + ")";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      if (reader.GetString(1) == column)
        return true;
    }
    return false;
  }
}
