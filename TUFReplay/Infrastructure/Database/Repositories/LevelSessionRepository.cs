using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class LevelSessionRepository
{
  public static void Save(LevelSession s)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"INSERT INTO level_sessions(
id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
song,author,artist,metadata_state
) VALUES(@id,@app,@tuf,@path,@open,@close,@tiles,@song,@author,@artist,@metadataState)";
    q.Parameters.AddWithValue("@id", s.Id);
    q.Parameters.AddWithValue("@app", s.AppSessionId);
    q.Parameters.AddWithValue("@tuf", DbValue.From(s.TufLevelId));
    q.Parameters.AddWithValue("@path", s.LevelPath);
    q.Parameters.AddWithValue("@open", s.OpenedAtUtc);
    q.Parameters.AddWithValue("@close", DbValue.From(s.ClosedAtUtc));
    q.Parameters.AddWithValue("@tiles", s.LevelTileCount);
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
    q.CommandText =
      @"
DELETE FROM level_sessions
WHERE id=@id
  AND NOT EXISTS (SELECT 1 FROM runs WHERE level_session_id=@id);";
    q.Parameters.AddWithValue("@id", id);
    int deleted = q.ExecuteNonQuery();

    if (deleted == 0)
    {
      q.CommandText = "UPDATE level_sessions SET closed_at_utc=@end WHERE id=@id";
      q.Parameters.AddWithValue("@end", end);
      q.ExecuteNonQuery();
    }

    transaction.Commit();
    return deleted > 0;
  }

  public static LevelSession Get(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"SELECT id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count,
song,author,artist,metadata_state FROM level_sessions WHERE id=@id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    return r.Read()
      ? new LevelSession
      {
        Id = r.GetString(0),
        AppSessionId = r.GetString(1),
        TufLevelId = DbValue.NullableInt(r, 2),
        LevelPath = r.GetString(3),
        OpenedAtUtc = r.GetString(4),
        ClosedAtUtc = DbValue.NullableString(r, 5),
        LevelTileCount = r.GetInt32(6),
        Song = DbValue.NullableString(r, 7),
        Author = DbValue.NullableString(r, 8),
        Artist = DbValue.NullableString(r, 9),
        MetadataState = (LevelMetadataState)r.GetInt32(10),
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
  }
}
