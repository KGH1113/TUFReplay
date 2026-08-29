using System;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static partial class ActivitySchema
{
  public const int Version = 15;

  public static void Ensure(SqliteConnection connection)
  {
    int version;
    using (SqliteCommand versionCommand = connection.CreateCommand())
    {
      versionCommand.CommandText = "PRAGMA user_version;";
      version = Convert.ToInt32(versionCommand.ExecuteScalar());
    }

    if (version > Version)
      throw new InvalidOperationException(
        "TUFReplay database schema is newer than this mod supports. version=" + version
      );

    if (version == 2)
    {
      Migrate(connection, "ALTER TABLE runs ADD COLUMN x_accuracy REAL; PRAGMA user_version = 3;");
      version = 3;
    }
    if (version == 3)
    {
      Migrate(
        connection,
        @"ALTER TABLE runs ADD COLUMN judgment_difficulty INTEGER;
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
        @"ALTER TABLE runs ADD COLUMN gameplay_hash BLOB;
ALTER TABLE runs ADD COLUMN gameplay_hash_version INTEGER;
PRAGMA user_version = 5;"
      );
      version = 5;
    }
    if (version == 5)
    {
      Migrate(
        connection,
        @"ALTER TABLE level_sessions ADD COLUMN song TEXT;
ALTER TABLE level_sessions ADD COLUMN author TEXT;
ALTER TABLE level_sessions ADD COLUMN artist TEXT;
ALTER TABLE level_sessions ADD COLUMN metadata_state INTEGER NOT NULL DEFAULT 0;
PRAGMA user_version = 6;"
      );
      version = 6;
    }
    if (version == 6)
    {
      AdvanceLegacyInputSchemaWithoutMutation(connection);
      version = 7;
    }
    if (version == 7)
    {
      Migrate(
        connection,
        @"CREATE TABLE microphone_recordings (
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
        @"ALTER TABLE microphone_recordings ADD COLUMN is_permanent INTEGER NOT NULL DEFAULT 1;
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
    if (version == 11)
    {
      Migrate(
        connection,
        @"CREATE TABLE gameplay_snapshots (
  gameplay_hash BLOB NOT NULL,
  gameplay_hash_version INTEGER NOT NULL,
  chart_json_utf8 BLOB NOT NULL,
  created_at_utc TEXT NOT NULL,
  PRIMARY KEY(gameplay_hash, gameplay_hash_version)
);
PRAGMA user_version = 12;"
      );
      version = 12;
    }
    if (version == 12)
    {
      MigrateLevels(connection);
      version = 13;
    }
    if (version == 13)
    {
      RepairRenamedForeignKeys(connection);
      version = 14;
    }
    if (version == 14)
    {
      Migrate(
        connection,
        @"CREATE TABLE IF NOT EXISTS gameplay_hash_migration_attempts (
  level_id TEXT PRIMARY KEY REFERENCES levels(id) ON DELETE CASCADE,
  file_size INTEGER,
  file_modified_utc_ticks INTEGER,
  result TEXT NOT NULL,
  attempted_at_utc TEXT NOT NULL
);
PRAGMA user_version = 15;"
      );
      version = 15;
    }

    if (version != 0 && version != Version)
      throw new InvalidOperationException("Unsupported TUFReplay database schema. version=" + version);

    if (version == 0)
      CreateCurrent(connection);
  }

  private static bool ForeignKeyTargetsTable(
    SqliteConnection connection,
    string table,
    string column,
    string targetTable
  )
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA foreign_key_list(" + table + ");";
    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      if (
        string.Equals(reader.GetString(3), column, StringComparison.OrdinalIgnoreCase)
        && string.Equals(reader.GetString(2), targetTable, StringComparison.OrdinalIgnoreCase)
      )
      {
        return true;
      }
    }

    return false;
  }

  private static void SetForeignKeys(SqliteConnection connection, bool enabled)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = enabled ? "PRAGMA foreign_keys=ON;" : "PRAGMA foreign_keys=OFF;";
    command.ExecuteNonQuery();
  }

  private static void EnsureNoForeignKeyViolations(SqliteConnection connection, SqliteTransaction transaction)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "PRAGMA foreign_key_check;";
    using SqliteDataReader violations = command.ExecuteReader();
    if (violations.Read())
      throw new InvalidOperationException("Database migration created a foreign-key violation.");
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

  internal static void AdvanceLegacyInputSchemaWithoutMutation(SqliteConnection connection)
  {
    Migrate(connection, "PRAGMA user_version = 7;");
  }
}
