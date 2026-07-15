using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class AppSessionRepository
{
  public static void Save(AppSession s)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      "INSERT INTO app_sessions(id,started_at_utc,ended_at_utc,recorder_time_zone_id,recorder_utc_offset_minutes) VALUES(@id,@start,@end,@tz,@offset)";
    q.Parameters.AddWithValue("@id", s.Id);
    q.Parameters.AddWithValue("@start", s.StartedAtUtc);
    q.Parameters.AddWithValue("@end", DbValue.From(s.EndedAtUtc));
    q.Parameters.AddWithValue("@tz", DbValue.From(s.RecorderTimeZoneId));
    q.Parameters.AddWithValue("@offset", s.RecorderUtcOffsetMinutes);
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
DELETE FROM app_sessions
WHERE id=@id
  AND NOT EXISTS (SELECT 1 FROM level_sessions WHERE app_session_id=@id);";
    q.Parameters.AddWithValue("@id", id);
    int deleted = q.ExecuteNonQuery();

    if (deleted == 0)
    {
      q.CommandText = "UPDATE app_sessions SET ended_at_utc=@end WHERE id=@id";
      q.Parameters.AddWithValue("@end", end);
      q.ExecuteNonQuery();
    }

    transaction.Commit();
    return deleted > 0;
  }

  public static List<AppSession> List(int offset, int limit)
  {
    var result = new List<AppSession>();
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      "SELECT id,started_at_utc,ended_at_utc,recorder_time_zone_id,recorder_utc_offset_minutes FROM app_sessions ORDER BY started_at_utc DESC,id DESC LIMIT @limit OFFSET @offset";
    q.Parameters.AddWithValue("@limit", limit);
    q.Parameters.AddWithValue("@offset", offset);
    using SqliteDataReader r = q.ExecuteReader();
    while (r.Read())
      result.Add(Read(r));
    return result;
  }

  private static AppSession Read(SqliteDataReader r) =>
    new AppSession
    {
      Id = r.GetString(0),
      StartedAtUtc = r.GetString(1),
      EndedAtUtc = DbValue.NullableString(r, 2),
      RecorderTimeZoneId = DbValue.NullableString(r, 3),
      RecorderUtcOffsetMinutes = r.GetInt32(4),
    };
}
