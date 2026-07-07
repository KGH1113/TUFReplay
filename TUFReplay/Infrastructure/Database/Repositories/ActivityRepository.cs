using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TUFReplay.Infrastructure.Database;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class ActivityRepository
{
  public static List<ActivityDaySummary> ListDaySummaries(string fromUtc = null, string toUtc = null)
  {
    List<ActivityDaySummary> days = new List<ActivityDaySummary>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = @"
SELECT substr(a.started_at_utc, 1, 10) AS activity_date,
       count(DISTINCT a.id),
       count(DISTINCT l.id),
       count(r.id),
       coalesce(sum(CASE WHEN r.no_fail_mode != 0 THEN 1 ELSE 0 END), 0),
       count(DISTINCT l.tuf_level_id),
       min(a.started_at_utc),
       max(coalesce(a.ended_at_utc, a.started_at_utc))
FROM app_sessions a
LEFT JOIN level_sessions l ON l.app_session_id = a.id
LEFT JOIN runs r ON r.level_session_id = l.id
WHERE (@from_utc IS NULL OR a.started_at_utc >= @from_utc)
  AND (@to_utc IS NULL OR a.started_at_utc < @to_utc)
GROUP BY activity_date
ORDER BY activity_date DESC;";

    command.Parameters.AddWithValue("@from_utc", DbValue.From(fromUtc));
    command.Parameters.AddWithValue("@to_utc", DbValue.From(toUtc));

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read())
    {
      days.Add(new ActivityDaySummary
      {
        Date = reader.GetString(0),
        AppSessionCount = reader.GetInt32(1),
        LevelSessionCount = reader.GetInt32(2),
        RunCount = reader.GetInt32(3),
        NoFailRunCount = reader.GetInt32(4),
        UniqueLevelCount = reader.GetInt32(5),
        StartedAtUtc = reader.GetString(6),
        EndedAtUtc = reader.GetString(7)
      });
    }

    return days;
  }

  public static LevelSessionOverview GetLevelSessionOverview(string levelSessionId)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectLevelSessionOverviewSql + @"
WHERE l.id = @level_session_id
GROUP BY l.id, l.app_session_id, l.tuf_level_id, l.opened_at_utc, l.closed_at_utc, l.level_tile_count;";

    command.Parameters.AddWithValue("@level_session_id", levelSessionId);

    using SqliteDataReader reader = command.ExecuteReader();
    return reader.Read() ? ReadLevelSessionOverview(reader) : null;
  }

  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSession(string appSessionId)
  {
    List<LevelSessionOverview> sessions = new List<LevelSessionOverview>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectLevelSessionOverviewSql + @"
WHERE l.app_session_id = @app_session_id
GROUP BY l.id, l.app_session_id, l.tuf_level_id, l.opened_at_utc, l.closed_at_utc, l.level_tile_count
ORDER BY l.opened_at_utc ASC;";

    command.Parameters.AddWithValue("@app_session_id", appSessionId);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) sessions.Add(ReadLevelSessionOverview(reader));

    return sessions;
  }

  public static List<LevelSessionOverview> ListLevelSessionOverviewsByDateRange(string fromUtc, string toUtc)
  {
    List<LevelSessionOverview> sessions = new List<LevelSessionOverview>();

    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();

    command.CommandText = SelectLevelSessionOverviewSql + @"
JOIN app_sessions a ON a.id = l.app_session_id
WHERE a.started_at_utc >= @from_utc
  AND a.started_at_utc < @to_utc
GROUP BY l.id, l.app_session_id, l.tuf_level_id, l.opened_at_utc, l.closed_at_utc, l.level_tile_count
ORDER BY l.opened_at_utc ASC;";

    command.Parameters.AddWithValue("@from_utc", fromUtc);
    command.Parameters.AddWithValue("@to_utc", toUtc);

    using SqliteDataReader reader = command.ExecuteReader();
    while (reader.Read()) sessions.Add(ReadLevelSessionOverview(reader));

    return sessions;
  }

  private const string SelectLevelSessionOverviewSql = @"
SELECT l.id,
       l.app_session_id,
       l.tuf_level_id,
       l.opened_at_utc,
       l.closed_at_utc,
       l.level_tile_count,
       count(r.id),
       coalesce(sum(CASE WHEN r.no_fail_mode != 0 THEN 1 ELSE 0 END), 0),
       min(r.start_tile),
       max(r.start_tile)
FROM level_sessions l
LEFT JOIN runs r ON r.level_session_id = l.id
";

  private static LevelSessionOverview ReadLevelSessionOverview(SqliteDataReader reader)
  {
    return new LevelSessionOverview
    {
      Id = reader.GetString(0),
      AppSessionId = reader.GetString(1),
      TufLevelId = reader.GetInt32(2),
      OpenedAtUtc = reader.GetString(3),
      ClosedAtUtc = DbValue.NullableString(reader, 4),
      LevelTileCount = reader.GetInt32(5),
      RunCount = reader.GetInt32(6),
      NoFailRunCount = reader.GetInt32(7),
      FirstStartTile = DbValue.NullableInt(reader, 8),
      LastStartTile = DbValue.NullableInt(reader, 9)
    };
  }
}
