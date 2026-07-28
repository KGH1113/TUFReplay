using System;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Unity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class LevelRepository
{
  public static string ResolveOrCreate(LevelRecord level)
  {
    if (level == null)
      throw new ArgumentNullException(nameof(level));

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    string id = ResolveOrCreate(connection, transaction, level);
    transaction.Commit();
    return id;
  }

  public static string ResolveOrCreate(SqliteConnection connection, SqliteTransaction transaction, LevelRecord level)
  {
    string identityKey = BuildIdentityKey(level);
    string candidateId = Guid.NewGuid().ToString("N");
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"
INSERT INTO levels(
  id,identity_key,source_kind,tuf_level_id,adofai_path,gameplay_hash,gameplay_hash_version,
  level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
) VALUES(
  @id,@identity,@source,@tuf,@path,@hash,@version,@tiles,@song,@author,@artist,@metadataState,@firstSeen,@lastSeen
)
ON CONFLICT(identity_key) DO UPDATE SET
  song=coalesce(excluded.song,levels.song),
  author=coalesce(excluded.author,levels.author),
  artist=coalesce(excluded.artist,levels.artist),
  metadata_state=max(levels.metadata_state,excluded.metadata_state),
  first_seen_at_utc=min(levels.first_seen_at_utc,excluded.first_seen_at_utc),
  last_seen_at_utc=max(levels.last_seen_at_utc,excluded.last_seen_at_utc);";
    command.Parameters.AddWithValue("@id", candidateId);
    command.Parameters.AddWithValue("@identity", identityKey);
    command.Parameters.AddWithValue("@source", (int)level.SourceKind);
    command.Parameters.AddWithValue("@tuf", DbValue.From(level.TufLevelId));
    command.Parameters.AddWithValue("@path", level.LevelPath);
    command.Parameters.AddWithValue("@hash", (object)level.GameplayHash ?? DBNull.Value);
    command.Parameters.AddWithValue("@version", DbValue.From(level.GameplayHashVersion));
    command.Parameters.AddWithValue("@tiles", level.LevelTileCount);
    command.Parameters.AddWithValue("@song", DbValue.From(level.Song));
    command.Parameters.AddWithValue("@author", DbValue.From(level.Author));
    command.Parameters.AddWithValue("@artist", DbValue.From(level.Artist));
    command.Parameters.AddWithValue("@metadataState", (int)level.MetadataState);
    command.Parameters.AddWithValue("@firstSeen", level.FirstSeenAtUtc);
    command.Parameters.AddWithValue("@lastSeen", level.LastSeenAtUtc);
    command.ExecuteNonQuery();

    command.Parameters.Clear();
    command.CommandText = "SELECT id FROM levels WHERE identity_key=@identity LIMIT 1";
    command.Parameters.AddWithValue("@identity", identityKey);
    return Convert.ToString(command.ExecuteScalar());
  }

  public static string PromoteToTufIdentity(string levelSessionId, int tufLevelId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    LevelSession session = LevelSessionRepository.Get(connection, transaction, levelSessionId);
    if (session == null)
      return null;
    if (session.SourceKind == LevelSourceKind.Tuf && session.TufLevelId == tufLevelId)
      return session.LevelId;

    string previousLevelId = session.LevelId;
    var promoted = FromSession(session);
    promoted.SourceKind = LevelSourceKind.Tuf;
    promoted.TufLevelId = tufLevelId;
    string levelId = ResolveOrCreate(connection, transaction, promoted);
    RepointSessions(connection, transaction, previousLevelId, levelId);
    DeleteIfOrphaned(connection, transaction, previousLevelId);
    transaction.Commit();
    return levelId;
  }

  public static string UpdateGameplayHashIfMissing(string levelId, byte[] hash, int version)
  {
    if (string.IsNullOrWhiteSpace(levelId) || hash == null)
      return levelId;

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    LevelRecord level = Get(connection, transaction, levelId);
    if (level == null || level.GameplayHash != null)
      return levelId;

    level.GameplayHash = hash;
    level.GameplayHashVersion = version;
    string resolvedId = ResolveOrCreate(connection, transaction, level);
    RepointSessions(connection, transaction, levelId, resolvedId);
    DeleteIfOrphaned(connection, transaction, levelId);
    transaction.Commit();
    return resolvedId;
  }

  public static void UpdateMetadata(string levelId, LevelMetadataSnapshot metadata, LevelMetadataState state)
  {
    if (string.IsNullOrWhiteSpace(levelId))
      return;
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"UPDATE levels SET song=@song,author=@author,artist=@artist,metadata_state=@state
WHERE id=@id AND metadata_state=@pending";
    command.Parameters.AddWithValue("@song", DbValue.From(metadata?.Song));
    command.Parameters.AddWithValue("@author", DbValue.From(metadata?.Author));
    command.Parameters.AddWithValue("@artist", DbValue.From(metadata?.Artist));
    command.Parameters.AddWithValue("@state", (int)state);
    command.Parameters.AddWithValue("@id", levelId);
    command.Parameters.AddWithValue("@pending", (int)LevelMetadataState.Pending);
    command.ExecuteNonQuery();
  }

  public static bool Exists(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM levels WHERE id=@id LIMIT 1";
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
    command.CommandText = "UPDATE levels SET last_seen_at_utc=max(last_seen_at_utc,@timestamp) WHERE id=@id";
    command.Parameters.AddWithValue("@timestamp", timestamp);
    command.Parameters.AddWithValue("@id", id);
    command.ExecuteNonQuery();
  }

  public static void DeleteIfOrphaned(SqliteConnection connection, SqliteTransaction transaction, string levelId)
  {
    if (string.IsNullOrWhiteSpace(levelId))
      return;
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      "DELETE FROM levels WHERE id=@id AND NOT EXISTS(SELECT 1 FROM level_sessions WHERE level_id=@id)";
    command.Parameters.AddWithValue("@id", levelId);
    command.ExecuteNonQuery();
  }

  private static LevelRecord Get(SqliteConnection connection, SqliteTransaction transaction, string id)
  {
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"SELECT id,identity_key,source_kind,tuf_level_id,adofai_path,gameplay_hash,gameplay_hash_version,
level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
FROM levels WHERE id=@id LIMIT 1";
    command.Parameters.AddWithValue("@id", id);
    using SqliteDataReader reader = command.ExecuteReader();
    if (!reader.Read())
      return null;
    return new LevelRecord
    {
      Id = reader.GetString(0),
      IdentityKey = reader.GetString(1),
      SourceKind = (LevelSourceKind)reader.GetInt32(2),
      TufLevelId = DbValue.NullableInt(reader, 3),
      LevelPath = reader.GetString(4),
      GameplayHash = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5),
      GameplayHashVersion = DbValue.NullableInt(reader, 6),
      LevelTileCount = reader.GetInt32(7),
      Song = DbValue.NullableString(reader, 8),
      Author = DbValue.NullableString(reader, 9),
      Artist = DbValue.NullableString(reader, 10),
      MetadataState = (LevelMetadataState)reader.GetInt32(11),
      FirstSeenAtUtc = reader.GetString(12),
      LastSeenAtUtc = reader.GetString(13),
    };
  }

  private static LevelRecord FromSession(LevelSession session) =>
    new LevelRecord
    {
      Id = session.LevelId,
      SourceKind = session.SourceKind,
      TufLevelId = session.TufLevelId,
      LevelPath = session.LevelPath,
      GameplayHash = session.GameplayHash,
      GameplayHashVersion = session.GameplayHashVersion,
      LevelTileCount = session.LevelTileCount,
      Song = session.Song,
      Author = session.Author,
      Artist = session.Artist,
      MetadataState = session.MetadataState,
      FirstSeenAtUtc = session.OpenedAtUtc,
      LastSeenAtUtc = session.ClosedAtUtc ?? session.OpenedAtUtc,
    };

  private static void RepointSessions(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string previousId,
    string nextId
  )
  {
    if (previousId == nextId)
      return;
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "UPDATE level_sessions SET level_id=@next WHERE level_id=@previous";
    command.Parameters.AddWithValue("@next", nextId);
    command.Parameters.AddWithValue("@previous", previousId);
    command.ExecuteNonQuery();
  }

  private static string BuildIdentityKey(LevelRecord level)
  {
    string canonicalPath =
      LevelPathIdentity.Canonicalize(level.LevelPath, requireExists: false) ?? level.LevelPath ?? string.Empty;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      canonicalPath = canonicalPath.ToUpperInvariant();
    string source = level.SourceKind == LevelSourceKind.Tuf ? "tuf:" + level.TufLevelId : "local";
    string pathIdentity =
      level.SourceKind == LevelSourceKind.Tuf ? string.Empty : canonicalPath.Length + ":" + canonicalPath + ":";
    string hash = GameplayChartHash.IsSupported(level.GameplayHashVersion, level.GameplayHash)
      ? level.GameplayHashVersion.Value + ":" + Convert.ToBase64String(level.GameplayHash)
      : "unknown:" + (level.Id ?? Guid.NewGuid().ToString("N"));
    return source + ":" + pathIdentity + hash;
  }
}
