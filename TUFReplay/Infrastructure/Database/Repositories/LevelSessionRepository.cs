using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class LevelSessionRepository
{
  public static void Save(LevelSession s)
  {
    if (string.IsNullOrWhiteSpace(s.LogicalLevelId))
      s.LogicalLevelId = LogicalLevelRepository.ResolveOrCreate(s);
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"INSERT INTO level_sessions(
id,logical_level_id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
level_file_hash,gameplay_hash,gameplay_hash_version,song,author,artist,metadata_state
) VALUES(@id,@logical,@app,@tuf,@path,@open,@close,@tiles,@levelFileHash,@gameplayHash,@gameplayVersion,@song,@author,@artist,@metadataState)";
    q.Parameters.AddWithValue("@id", s.Id);
    q.Parameters.AddWithValue("@logical", s.LogicalLevelId);
    q.Parameters.AddWithValue("@app", s.AppSessionId);
    q.Parameters.AddWithValue("@tuf", DbValue.From(s.TufLevelId));
    q.Parameters.AddWithValue("@path", s.LevelPath);
    q.Parameters.AddWithValue("@open", s.OpenedAtUtc);
    q.Parameters.AddWithValue("@close", DbValue.From(s.ClosedAtUtc));
    q.Parameters.AddWithValue("@tiles", s.LevelTileCount);
    q.Parameters.AddWithValue("@levelFileHash", (object)s.LevelFileHash ?? System.DBNull.Value);
    q.Parameters.AddWithValue("@gameplayHash", (object)s.GameplayHash ?? System.DBNull.Value);
    q.Parameters.AddWithValue("@gameplayVersion", DbValue.From(s.GameplayHashVersion));
    q.Parameters.AddWithValue("@song", DbValue.From(s.Song));
    q.Parameters.AddWithValue("@author", DbValue.From(s.Author));
    q.Parameters.AddWithValue("@artist", DbValue.From(s.Artist));
    q.Parameters.AddWithValue("@metadataState", (int)s.MetadataState);
    q.ExecuteNonQuery();
  }

  public static bool CloseOrDeleteIfEmpty(string id, string end)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteTransaction transaction = c.BeginTransaction();
    using SqliteCommand q = c.CreateCommand();
    q.Transaction = transaction;
    q.CommandText = "SELECT logical_level_id FROM level_sessions WHERE id=@id";
    q.Parameters.AddWithValue("@id", id);
    string logicalLevelId = q.ExecuteScalar() as string;
    q.CommandText =
      @"
DELETE FROM level_sessions
WHERE id=@id
  AND NOT EXISTS (SELECT 1 FROM runs WHERE level_session_id=@id);";
    int deleted = q.ExecuteNonQuery();

    if (deleted == 0)
    {
      q.CommandText = "UPDATE level_sessions SET closed_at_utc=@end WHERE id=@id";
      q.Parameters.AddWithValue("@end", end);
      q.ExecuteNonQuery();
      LogicalLevelRepository.TouchLastSeen(c, transaction, logicalLevelId, end);
    }

    if (deleted > 0)
      LogicalLevelRepository.DeleteIfOrphaned(c, transaction, logicalLevelId);

    transaction.Commit();
    return deleted > 0;
  }

  public static LevelSession Get(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"SELECT id,logical_level_id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
level_file_hash,gameplay_hash,gameplay_hash_version,song,author,artist,metadata_state FROM level_sessions WHERE id=@id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    return r.Read()
      ? new LevelSession
      {
        Id = r.GetString(0),
        LogicalLevelId = r.GetString(1),
        AppSessionId = r.GetString(2),
        TufLevelId = DbValue.NullableInt(r, 3),
        LevelPath = r.GetString(4),
        OpenedAtUtc = r.GetString(5),
        ClosedAtUtc = DbValue.NullableString(r, 6),
        LevelTileCount = r.GetInt32(7),
        LevelFileHash = r.IsDBNull(8) ? null : (byte[])r.GetValue(8),
        GameplayHash = r.IsDBNull(9) ? null : (byte[])r.GetValue(9),
        GameplayHashVersion = DbValue.NullableInt(r, 10),
        Song = DbValue.NullableString(r, 11),
        Author = DbValue.NullableString(r, 12),
        Artist = DbValue.NullableString(r, 13),
        MetadataState = (LevelMetadataState)r.GetInt32(14),
      }
      : null;
  }

  public static bool Exists(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText = "SELECT 1 FROM level_sessions WHERE id=@id LIMIT 1";
    q.Parameters.AddWithValue("@id", id);
    return q.ExecuteScalar() != null;
  }

  public static List<LevelSession> ListByLogicalLevelNewestFirst(string id)
  {
    var result = new List<LevelSession>();
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"SELECT id,logical_level_id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
level_file_hash,gameplay_hash,gameplay_hash_version,song,author,artist,metadata_state
FROM level_sessions WHERE logical_level_id=@id ORDER BY opened_at_utc DESC,id DESC";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    while (r.Read())
    {
      result.Add(
        new LevelSession
        {
          Id = r.GetString(0),
          LogicalLevelId = r.GetString(1),
          AppSessionId = r.GetString(2),
          TufLevelId = DbValue.NullableInt(r, 3),
          LevelPath = r.GetString(4),
          OpenedAtUtc = r.GetString(5),
          ClosedAtUtc = DbValue.NullableString(r, 6),
          LevelTileCount = r.GetInt32(7),
          LevelFileHash = r.IsDBNull(8) ? null : (byte[])r.GetValue(8),
          GameplayHash = r.IsDBNull(9) ? null : (byte[])r.GetValue(9),
          GameplayHashVersion = DbValue.NullableInt(r, 10),
          Song = DbValue.NullableString(r, 11),
          Author = DbValue.NullableString(r, 12),
          Artist = DbValue.NullableString(r, 13),
          MetadataState = (LevelMetadataState)r.GetInt32(14),
        }
      );
    }
    return result;
  }

  public static void UpdateMetadata(string id, LevelMetadataSnapshot metadata, LevelMetadataState state)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"UPDATE level_sessions
SET song=@song,author=@author,artist=@artist,metadata_state=@state
WHERE id=@id AND metadata_state=@pending";
    q.Parameters.AddWithValue("@song", DbValue.From(metadata?.Song));
    q.Parameters.AddWithValue("@author", DbValue.From(metadata?.Author));
    q.Parameters.AddWithValue("@artist", DbValue.From(metadata?.Artist));
    q.Parameters.AddWithValue("@state", (int)state);
    q.Parameters.AddWithValue("@id", id);
    q.Parameters.AddWithValue("@pending", (int)LevelMetadataState.Pending);
    q.ExecuteNonQuery();
    LevelSession session = Get(id);
    LogicalLevelRepository.UpdateMetadata(session?.LogicalLevelId, metadata);
  }

  public static string UpdateTufLevelIdIfMissing(string id, int tufLevelId)
  {
    LevelSession session = Get(id);
    if (session == null)
      return null;
    if (session.TufLevelId.HasValue)
      return session.LogicalLevelId;
    return LogicalLevelRepository.PromoteToTufIdentity(id, tufLevelId);
  }
}
