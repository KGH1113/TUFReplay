using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace TUFReplay.Activity.Migrations;

public static class LegacyActivityV15Importer
{
  public const int LegacyVersion = 15;

  public static void EnsureCurrent(string mainPath, string migratingPath, string backupPath)
  {
    ReplacementRecovery recovery = RecoverInterruptedReplacement(mainPath, migratingPath, backupPath);
    if (recovery == ReplacementRecovery.Completed)
      return;
    if (recovery == ReplacementRecovery.RestoredBackup)
      throw new InvalidOperationException(
        "An interrupted TUFReplay 0.2 migration was rolled back. The pre-0.2 database was restored; restart to retry."
      );
    if (!File.Exists(mainPath))
    {
      CreateCurrent(mainPath);
      return;
    }

    using (SqliteConnection source = Open(mainPath))
    {
      ActivitySchema.ReadHeader(source, out int applicationId, out int version);
      if (applicationId == ActivitySchema.ApplicationId && version == ActivitySchema.Version)
      {
        ActivitySchema.Validate(source);
        return;
      }
      if (applicationId != 0 || version != LegacyVersion)
        throw new InvalidOperationException(
          "TUFReplay 0.2 can import only a schema v15 database. Run 0.1.0-beta.10 first. detected=" + version
        );
      Checkpoint(source);
    }

    if (File.Exists(backupPath))
      throw new IOException("The TUFReplay pre-0.2 database backup already exists: " + backupPath);
    DeleteIfExists(migratingPath);
    CreateCurrent(migratingPath);
    Import(mainPath, migratingPath);
    if (!IsCurrentFile(migratingPath))
      throw new InvalidDataException("The imported TUFReplay activity database has an invalid generation header.");

    File.Move(mainPath, backupPath);
    try
    {
      File.Move(migratingPath, mainPath);
      if (!IsCurrentFile(mainPath))
        throw new InvalidDataException("The replaced TUFReplay activity database is invalid.");
    }
    catch
    {
      if (File.Exists(mainPath) && !IsCurrentFile(mainPath))
        File.Delete(mainPath);
      if (!File.Exists(mainPath) && File.Exists(backupPath))
        File.Move(backupPath, mainPath);
      throw;
    }
  }

  public static bool IsLegacyV15(string path)
  {
    if (!File.Exists(path))
      return false;
    using SqliteConnection connection = Open(path);
    ActivitySchema.ReadHeader(connection, out int applicationId, out int version);
    return applicationId == 0 && version == LegacyVersion;
  }

  private static ReplacementRecovery RecoverInterruptedReplacement(
    string mainPath,
    string migratingPath,
    string backupPath
  )
  {
    if (File.Exists(mainPath))
      return ReplacementRecovery.None;
    if (!File.Exists(backupPath))
      return ReplacementRecovery.None;

    if (IsCurrentFile(migratingPath))
    {
      File.Move(migratingPath, mainPath);
      return ReplacementRecovery.Completed;
    }

    DeleteIfExists(migratingPath);
    File.Move(backupPath, mainPath);
    return ReplacementRecovery.RestoredBackup;
  }

  private static void CreateCurrent(string path)
  {
    using SqliteConnection connection = Open(path);
    ActivitySchema.Ensure(connection);
    ActivitySchema.Validate(connection);
  }

  private static void Import(string sourcePath, string targetPath)
  {
    using SqliteConnection target = Open(targetPath);
    using SqliteCommand attach = target.CreateCommand();
    attach.CommandText = "ATTACH DATABASE @source AS legacy";
    attach.Parameters.AddWithValue("@source", sourcePath);
    attach.ExecuteNonQuery();
    try
    {
      using SqliteTransaction transaction = target.BeginTransaction();
      using SqliteCommand command = target.CreateCommand();
      command.Transaction = transaction;
      command.CommandText =
        @"INSERT INTO app_sessions SELECT id,started_at_utc,ended_at_utc,recorder_time_zone_id,recorder_utc_offset_minutes FROM legacy.app_sessions;
INSERT INTO levels(
  id,identity_key,source_kind,tuf_level_id,adofai_path,gameplay_hash,gameplay_hash_version,
  level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
)
SELECT id,'legacy:'||id,source_kind,tuf_level_id,adofai_path,NULL,NULL,
       level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
FROM legacy.levels;
INSERT INTO level_sessions SELECT id,level_id,app_session_id,opened_at_utc,closed_at_utc FROM legacy.level_sessions;
INSERT INTO runs(
  id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
  gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
  judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
  judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,input_count,hit_context_count,
  replay_unavailable_reason
)
SELECT id,level_session_id,run_index,started_at_utc,ended_at_utc,start_tile,last_tile,result,no_fail_mode,
       gameplay_start_song_position,level_pitch_percent,effective_pitch,x_accuracy,judgment_difficulty,
       judgment_overload,judgment_too_early,judgment_early,judgment_early_perfect,judgment_perfect,
       judgment_late_perfect,judgment_late,judgment_too_late,judgment_miss,input_count,hit_context_count,
       'legacy_engine'
FROM legacy.runs;";
      command.ExecuteNonQuery();
      AssertSameCount(target, transaction, "app_sessions");
      AssertSameCount(target, transaction, "levels");
      AssertSameCount(target, transaction, "level_sessions");
      AssertSameCount(target, transaction, "runs");
      transaction.Commit();
    }
    finally
    {
      using SqliteCommand detach = target.CreateCommand();
      detach.CommandText = "DETACH DATABASE legacy";
      detach.ExecuteNonQuery();
    }
    ActivitySchema.Validate(target);
  }

  private static void AssertSameCount(SqliteConnection connection, SqliteTransaction transaction, string table)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      "SELECT (SELECT count(*) FROM main." + table + ")=(SELECT count(*) FROM legacy." + table + ")";
    if (Convert.ToInt32(command.ExecuteScalar()) == 0)
      throw new InvalidOperationException("TUFReplay activity import count mismatch: " + table);
  }

  private static bool IsCurrentFile(string path)
  {
    if (!File.Exists(path))
      return false;
    try
    {
      using SqliteConnection connection = Open(path);
      if (!ActivitySchema.IsCurrent(connection))
        return false;
      ActivitySchema.Validate(connection);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static void Checkpoint(SqliteConnection connection)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
    command.ExecuteNonQuery();
  }

  private static SqliteConnection Open(string path)
  {
    var connection = new SqliteConnection("Data Source=" + path + ";Default Timeout=5;Pooling=False");
    connection.Open();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
    command.ExecuteNonQuery();
    return connection;
  }

  private static void DeleteIfExists(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }

  private enum ReplacementRecovery
  {
    None,
    Completed,
    RestoredBackup,
  }
}
