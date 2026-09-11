using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static partial class ActivitySchema
{
  public const int ApplicationId = 0x54554652;
  public const int Version = 1;

  public static void Ensure(SqliteConnection connection)
  {
    ReadHeader(connection, out int applicationId, out int version);
    if (applicationId == 0 && version == 0)
    {
      CreateCurrent(connection);
      return;
    }

    if (applicationId != ApplicationId || version != Version)
      throw new InvalidOperationException(
        "Unsupported TUFReplay activity database. applicationId=" + applicationId + ", version=" + version
      );
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
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA integrity_check;";
    if (!string.Equals(Convert.ToString(command.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("TUFReplay activity database integrity check failed.");

    command.CommandText = "PRAGMA foreign_key_check;";
    using SqliteDataReader violations = command.ExecuteReader();
    if (violations.Read())
      throw new InvalidOperationException("TUFReplay activity database contains a foreign-key violation.");
  }
}
