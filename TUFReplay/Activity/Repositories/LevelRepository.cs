using System;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Shared.Database;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Activity.Repositories;

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
    // Rows saved under an older identity of this chart move to the current one, so a
    // chart keeps one history when its key changes (V4 to V5, or pre-path TUF keys).
    PromoteIdentity(connection, transaction, V4IdentityKey(level), null, level, identityKey);
    PromoteIdentity(connection, transaction, PathlessTufIdentityKey(level), level.LevelPath, level, identityKey);
    string candidateId = Guid.NewGuid().ToString("N");
    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      @"
INSERT INTO levels(
  id,identity_key,source_kind,tuf_level_id,adofai_path,gameplay_hash,gameplay_hash_version,gameplay_hash_v5,
  level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc
) VALUES(
  @id,@identity,@source,@tuf,@path,@hash,@version,@hashV5,@tiles,@song,@author,@artist,@metadataState,@firstSeen,@lastSeen
)
ON CONFLICT(identity_key) DO UPDATE SET
  gameplay_hash_v5=coalesce(levels.gameplay_hash_v5,excluded.gameplay_hash_v5),
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
    command.Parameters.AddWithValue("@hashV5", (object)level.GameplayHashV5 ?? DBNull.Value);
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

  /// <summary>
  /// Fills the hashes a level row lacks: both for a row saved without any, and V5 for a
  /// row whose V4 equals these hashes' V4. The caller must have hashed the recorded file.
  /// </summary>
  public static string UpdateGameplayHashesIfMissing(string levelId, GameplayChartHashes hashes)
  {
    if (string.IsNullOrWhiteSpace(levelId) || hashes?.V4 == null)
      return levelId;

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = connection.BeginTransaction();
    LevelRecord level = Get(connection, transaction, levelId);
    if (level == null)
      return levelId;

    bool changed = false;
    if (level.GameplayHash == null)
    {
      level.GameplayHash = hashes.V4;
      level.GameplayHashVersion = GameplayChartHash.Version;
      changed = true;
    }
    if (GameplayChartHashMatch.CanTakeV5(level.GameplayHash, level.GameplayHashVersion, level.GameplayHashV5, hashes))
    {
      level.GameplayHashV5 = hashes.V5;
      changed = true;
    }
    if (!changed)
      return levelId;

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
level_tile_count,song,author,artist,metadata_state,first_seen_at_utc,last_seen_at_utc,gameplay_hash_v5
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
      GameplayHashV5 = reader.IsDBNull(14) ? null : (byte[])reader.GetValue(14),
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
      GameplayHashV5 = session.GameplayHashV5,
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
    if (GameplayChartHash.IsV5(level.GameplayHashV5))
      return IdentityPrefix(level) + GameplayChartHash.V5Version + ":" + Convert.ToBase64String(level.GameplayHashV5);
    return V4IdentityKey(level) ?? IdentityPrefix(level) + "unknown:" + (level.Id ?? Guid.NewGuid().ToString("N"));
  }

  // The identity every row had before V5: source, path and V4 hash.
  private static string V4IdentityKey(LevelRecord level) =>
    GameplayChartHash.IsSupported(level.GameplayHashVersion, level.GameplayHash)
      ? IdentityPrefix(level) + level.GameplayHashVersion.Value + ":" + Convert.ToBase64String(level.GameplayHash)
      : null;

  // TUF rows saved before identities included the file path.
  private static string PathlessTufIdentityKey(LevelRecord level) =>
    level.SourceKind == LevelSourceKind.Tuf
    && level.TufLevelId.HasValue
    && GameplayChartHash.IsSupported(level.GameplayHashVersion, level.GameplayHash)
      ? "tuf:"
        + level.TufLevelId.Value
        + ":"
        + level.GameplayHashVersion.Value
        + ":"
        + Convert.ToBase64String(level.GameplayHash)
      : null;

  private static string IdentityPrefix(LevelRecord level)
  {
    string canonicalPath =
      LevelPathIdentity.Canonicalize(level.LevelPath, requireExists: false) ?? level.LevelPath ?? string.Empty;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      canonicalPath = canonicalPath.ToUpperInvariant();
    string source = level.SourceKind == LevelSourceKind.Tuf ? "tuf:" + level.TufLevelId : "local";
    return source + ":" + canonicalPath.Length + ":" + canonicalPath + ":";
  }

  /// <summary>
  /// Moves the row saved under <paramref name="fromKey"/> to <paramref name="toKey"/>, or
  /// merges it into the row that already has <paramref name="toKey"/>.
  /// </summary>
  private static void PromoteIdentity(
    SqliteConnection connection,
    SqliteTransaction transaction,
    string fromKey,
    string requiredPath,
    LevelRecord level,
    string toKey
  )
  {
    if (fromKey == null || string.Equals(fromKey, toKey, StringComparison.Ordinal))
      return;

    using SqliteCommand command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText =
      requiredPath == null
        ? "SELECT id FROM levels WHERE identity_key=@from LIMIT 1"
        : "SELECT id FROM levels WHERE identity_key=@from AND adofai_path=@path LIMIT 1";
    command.Parameters.AddWithValue("@from", fromKey);
    if (requiredPath != null)
      command.Parameters.AddWithValue("@path", requiredPath);
    if (!(command.ExecuteScalar() is string fromId))
      return;

    command.Parameters.Clear();
    command.CommandText = "SELECT id FROM levels WHERE identity_key=@to LIMIT 1";
    command.Parameters.AddWithValue("@to", toKey);
    string toId = command.ExecuteScalar() as string;

    command.Parameters.Clear();
    command.Parameters.AddWithValue("@hashV5", (object)level.GameplayHashV5 ?? DBNull.Value);
    if (toId == null)
    {
      command.CommandText =
        "UPDATE levels SET identity_key=@to,gameplay_hash_v5=coalesce(gameplay_hash_v5,@hashV5) WHERE id=@from";
      command.Parameters.AddWithValue("@to", toKey);
      command.Parameters.AddWithValue("@from", fromId);
      command.ExecuteNonQuery();
      return;
    }

    command.CommandText =
      @"UPDATE levels SET
  gameplay_hash_v5=coalesce(gameplay_hash_v5,@hashV5),
  song=coalesce(song,(SELECT song FROM levels WHERE id=@from)),
  author=coalesce(author,(SELECT author FROM levels WHERE id=@from)),
  artist=coalesce(artist,(SELECT artist FROM levels WHERE id=@from)),
  metadata_state=max(metadata_state,(SELECT metadata_state FROM levels WHERE id=@from)),
  first_seen_at_utc=min(first_seen_at_utc,(SELECT first_seen_at_utc FROM levels WHERE id=@from)),
  last_seen_at_utc=max(last_seen_at_utc,(SELECT last_seen_at_utc FROM levels WHERE id=@from))
WHERE id=@to";
    command.Parameters.AddWithValue("@from", fromId);
    command.Parameters.AddWithValue("@to", toId);
    command.ExecuteNonQuery();
    RepointSessions(connection, transaction, fromId, toId);
    DeleteIfOrphaned(connection, transaction, fromId);
  }
}
