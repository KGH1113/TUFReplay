using System;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Unity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class LogicalLevelRepository
{
  public static string ResolveOrCreate(LevelSession level)
  {
    if (level == null)
      throw new ArgumentNullException(nameof(level));

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    string id = ResolveOrCreate(connection, transaction, level);
    transaction.Commit();
    return id;
  }

  public static string ResolveOrCreate(
    SqliteConnection connection,
    SqliteTransaction transaction,
    LevelSession level
  )
  {
    string identityKey = BuildIdentityKey(level);
    string candidateId = Guid.NewGuid().ToString("N");
    string lastSeen = level.ClosedAtUtc ?? level.OpenedAtUtc;
    using (SqliteCommand command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText =
        @"
INSERT INTO logical_levels(
  id,identity_key,tuf_level_id,gameplay_hash,gameplay_hash_version,
  song,author,artist,first_seen_at_utc,last_seen_at_utc
) VALUES(@id,@identity,@tuf,@gameplayHash,@gameplayVersion,@song,@author,@artist,@firstSeen,@lastSeen)
ON CONFLICT(identity_key) DO UPDATE SET
  tuf_level_id=coalesce(excluded.tuf_level_id,logical_levels.tuf_level_id),
  gameplay_hash=coalesce(excluded.gameplay_hash,logical_levels.gameplay_hash),
  gameplay_hash_version=coalesce(excluded.gameplay_hash_version,logical_levels.gameplay_hash_version),
  song=coalesce(excluded.song,logical_levels.song),
  author=coalesce(excluded.author,logical_levels.author),
  artist=coalesce(excluded.artist,logical_levels.artist),
  first_seen_at_utc=min(logical_levels.first_seen_at_utc,excluded.first_seen_at_utc),
  last_seen_at_utc=max(logical_levels.last_seen_at_utc,excluded.last_seen_at_utc);";
      command.Parameters.AddWithValue("@id", candidateId);
      command.Parameters.AddWithValue("@identity", identityKey);
      command.Parameters.AddWithValue("@tuf", DbValue.From(level.TufLevelId));
      command.Parameters.AddWithValue("@gameplayHash", (object)level.GameplayHash ?? DBNull.Value);
      command.Parameters.AddWithValue("@gameplayVersion", DbValue.From(level.GameplayHashVersion));
      command.Parameters.AddWithValue("@song", DbValue.From(level.Song));
      command.Parameters.AddWithValue("@author", DbValue.From(level.Author));
      command.Parameters.AddWithValue("@artist", DbValue.From(level.Artist));
      command.Parameters.AddWithValue("@firstSeen", level.OpenedAtUtc);
      command.Parameters.AddWithValue("@lastSeen", lastSeen);
      command.ExecuteNonQuery();

      command.Parameters.Clear();
      command.CommandText = "SELECT id FROM logical_levels WHERE identity_key=@identity LIMIT 1";
      command.Parameters.AddWithValue("@identity", identityKey);
      return Convert.ToString(command.ExecuteScalar());
    }
  }

  public static string PromoteToTufIdentity(string levelSessionId, int tufLevelId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    LevelSession level = ReadLevelSession(connection, transaction, levelSessionId);
    if (level == null)
      return null;

    string previousLogicalLevelId = level.LogicalLevelId;
    level.TufLevelId = tufLevelId;
    string logicalLevelId = ResolveOrCreate(connection, transaction, level);
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"UPDATE level_sessions SET
tuf_level_id=CASE WHEN id=@id THEN @tuf ELSE tuf_level_id END,
logical_level_id=@logical
WHERE logical_level_id=@previous";
    command.Parameters.AddWithValue("@tuf", tufLevelId);
    command.Parameters.AddWithValue("@logical", logicalLevelId);
    command.Parameters.AddWithValue("@id", levelSessionId);
    command.Parameters.AddWithValue("@previous", previousLogicalLevelId);
    command.ExecuteNonQuery();
    DeleteIfOrphaned(connection, transaction, previousLogicalLevelId);
    transaction.Commit();
    return logicalLevelId;
  }

  public static void UpdateMetadata(string logicalLevelId, LevelMetadataSnapshot metadata)
  {
    if (string.IsNullOrWhiteSpace(logicalLevelId) || metadata == null)
      return;
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"UPDATE logical_levels
SET song=coalesce(@song,song),author=coalesce(@author,author),artist=coalesce(@artist,artist)
WHERE id=@id";
    command.Parameters.AddWithValue("@song", DbValue.From(metadata.Song));
    command.Parameters.AddWithValue("@author", DbValue.From(metadata.Author));
    command.Parameters.AddWithValue("@artist", DbValue.From(metadata.Artist));
    command.Parameters.AddWithValue("@id", logicalLevelId);
    command.ExecuteNonQuery();
  }

  public static bool Exists(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM logical_levels WHERE id=@id LIMIT 1";
    command.Parameters.AddWithValue("@id", id);
    return command.ExecuteScalar() != null;
  }

  public static void TouchLastSeen(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string id,
    string timestamp
  )
  {
    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(timestamp))
      return;
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      "UPDATE logical_levels SET last_seen_at_utc=max(last_seen_at_utc,@timestamp) WHERE id=@id";
    command.Parameters.AddWithValue("@timestamp", timestamp);
    command.Parameters.AddWithValue("@id", id);
    command.ExecuteNonQuery();
  }

  public static void DeleteIfOrphaned(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string logicalLevelId
  )
  {
    if (string.IsNullOrWhiteSpace(logicalLevelId))
      return;
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"DELETE FROM logical_levels
WHERE id=@id AND NOT EXISTS(SELECT 1 FROM level_sessions WHERE logical_level_id=@id)";
    command.Parameters.AddWithValue("@id", logicalLevelId);
    command.ExecuteNonQuery();
  }

  private static string BuildIdentityKey(LevelSession level)
  {
    if (level.TufLevelId.HasValue)
      return "tuf:" + level.TufLevelId.Value;

    if (GameplayChartHash.IsSupported(level.GameplayHashVersion, level.GameplayHash))
      return "gameplay:" + level.GameplayHashVersion.Value + ":" + Convert.ToBase64String(level.GameplayHash);

    string canonicalPath = LevelPathIdentity.Canonicalize(level.LevelPath, requireExists: false) ?? level.LevelPath ?? "";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      canonicalPath = canonicalPath.ToUpperInvariant();
    string fileFingerprint =
      level.LevelFileHash == null
        ? "unknown:" + level.Id
        : Convert.ToBase64String(level.LevelFileHash);
    return "file:" + canonicalPath.Length + ":" + canonicalPath + ":" + fileFingerprint;
  }

  private static LevelSession ReadLevelSession(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string id
  )
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"SELECT id,logical_level_id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,
level_tile_count,level_file_hash,gameplay_hash,gameplay_hash_version,song,author,artist,metadata_state
FROM level_sessions WHERE id=@id";
    command.Parameters.AddWithValue("@id", id);
    using SqliteDataReader reader = command.ExecuteReader();
    if (!reader.Read())
      return null;
    return new LevelSession
    {
      Id = reader.GetString(0),
      LogicalLevelId = DbValue.NullableString(reader, 1),
      AppSessionId = reader.GetString(2),
      TufLevelId = DbValue.NullableInt(reader, 3),
      LevelPath = reader.GetString(4),
      OpenedAtUtc = reader.GetString(5),
      ClosedAtUtc = DbValue.NullableString(reader, 6),
      LevelTileCount = reader.GetInt32(7),
      LevelFileHash = reader.IsDBNull(8) ? null : (byte[])reader.GetValue(8),
      GameplayHash = reader.IsDBNull(9) ? null : (byte[])reader.GetValue(9),
      GameplayHashVersion = DbValue.NullableInt(reader, 10),
      Song = DbValue.NullableString(reader, 11),
      Author = DbValue.NullableString(reader, 12),
      Artist = DbValue.NullableString(reader, 13),
      MetadataState = (LevelMetadataState)reader.GetInt32(14),
    };
  }
}
