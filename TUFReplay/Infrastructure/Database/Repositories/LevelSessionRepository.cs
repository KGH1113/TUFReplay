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
      "INSERT INTO level_sessions(id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count) VALUES(@id,@app,@tuf,@path,@open,@close,@tiles)";
    q.Parameters.AddWithValue("@id", s.Id);
    q.Parameters.AddWithValue("@app", s.AppSessionId);
    q.Parameters.AddWithValue("@tuf", DbValue.From(s.TufLevelId));
    q.Parameters.AddWithValue("@path", s.LevelPath);
    q.Parameters.AddWithValue("@open", s.OpenedAtUtc);
    q.Parameters.AddWithValue("@close", DbValue.From(s.ClosedAtUtc));
    q.Parameters.AddWithValue("@tiles", s.LevelTileCount);
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
      "SELECT id,app_session_id,tuf_level_id,level_path,opened_at_utc,closed_at_utc,level_tile_count FROM level_sessions WHERE id=@id";
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
      }
      : null;
  }
}
