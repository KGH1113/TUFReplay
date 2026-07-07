using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class LevelSessionRepository
{
  public static void Save(LevelSession session)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
INSERT OR REPLACE INTO level_sessions
(id, app_session_id, tuf_level_id, opened_at_utc, closed_at_utc, level_tile_count)
VALUES
(@id, @app_session_id, @tuf_level_id, @opened_at_utc, @closed_at_utc, @level_tile_count);";

    AddSessionParameters(command, session);
    command.ExecuteNonQuery();
  }

  public static void Close(string id, string closedAtUtc)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
UPDATE level_sessions
SET closed_at_utc = @closed_at_utc
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@closed_at_utc", DbValue.From(closedAtUtc));
    command.ExecuteNonQuery();
  }

  public static LevelSession Get(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, app_session_id, tuf_level_id, opened_at_utc, closed_at_utc, level_tile_count
FROM level_sessions
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadSession(reader) : null;
  }

  public static List<LevelSession> ListByAppSession(string appSessionId)
  {
    List<LevelSession> sessions = new List<LevelSession>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, app_session_id, tuf_level_id, opened_at_utc, closed_at_utc, level_tile_count
FROM level_sessions
WHERE app_session_id = @app_session_id
ORDER BY opened_at_utc ASC;";

    command.Parameters.AddWithValue("@app_session_id", appSessionId);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) sessions.Add(ReadSession(reader));

    return sessions;
  }

  private static void AddSessionParameters(SqliteCommand command, LevelSession session)
  {
    command.Parameters.AddWithValue("@id", session.Id);
    command.Parameters.AddWithValue("@app_session_id", session.AppSessionId);
    command.Parameters.AddWithValue("@tuf_level_id", session.TufLevelId);
    command.Parameters.AddWithValue("@opened_at_utc", session.OpenedAtUtc);
    command.Parameters.AddWithValue("@closed_at_utc", DbValue.From(session.ClosedAtUtc));
    command.Parameters.AddWithValue("@level_tile_count", session.LevelTileCount);
  }

  private static LevelSession ReadSession(SqliteDataReader reader)
  {
    return new LevelSession
    {
      Id = reader.GetString(0),
      AppSessionId = reader.GetString(1),
      TufLevelId = reader.GetInt32(2),
      OpenedAtUtc = reader.GetString(3),
      ClosedAtUtc = DbValue.NullableString(reader, 4),
      LevelTileCount = reader.GetInt32(5)
    };
  }
}
