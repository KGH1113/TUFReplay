using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class AppSessionRepository
{
  public static void Save(AppSession session)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
INSERT OR REPLACE INTO app_sessions
(id, started_at_utc, ended_at_utc)
VALUES
(@id, @started_at_utc, @ended_at_utc);";

    command.Parameters.AddWithValue("@id", session.Id);
    command.Parameters.AddWithValue("@started_at_utc", session.StartedAtUtc);
    command.Parameters.AddWithValue("@ended_at_utc", DbValue.From(session.EndedAtUtc));

    command.ExecuteNonQuery();
  }

  public static void Close(string id, string endedAtUtc)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
UPDATE app_sessions
SET ended_at_utc = @ended_at_utc
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);
    command.Parameters.AddWithValue("@ended_at_utc", DbValue.From(endedAtUtc));

    command.ExecuteNonQuery();
  }

  public static AppSession Get(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, started_at_utc, ended_at_utc
FROM app_sessions
WHERE id = @id;";

    command.Parameters.AddWithValue("@id", id);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadSession(reader) : null;
  }

  public static List<AppSession> List(string fromUtc = null, string toUtc = null)
  {
    List<AppSession> sessions = new List<AppSession>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT id, started_at_utc, ended_at_utc
FROM app_sessions
WHERE (@from_utc IS NULL OR started_at_utc >= @from_utc)
  AND (@to_utc IS NULL OR started_at_utc < @to_utc)
ORDER BY started_at_utc DESC;";

    command.Parameters.AddWithValue("@from_utc", DbValue.From(fromUtc));
    command.Parameters.AddWithValue("@to_utc", DbValue.From(toUtc));

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) sessions.Add(ReadSession(reader));

    return sessions;
  }

  private static AppSession ReadSession(SqliteDataReader reader)
  {
    return new AppSession
    {
      Id = reader.GetString(0),
      StartedAtUtc = reader.GetString(1),
      EndedAtUtc = DbValue.NullableString(reader, 2)
    };
  }
}
