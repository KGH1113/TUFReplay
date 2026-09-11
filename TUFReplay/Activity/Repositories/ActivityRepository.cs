using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Models;
using TUFReplay.Shared.Database;
using DatabaseStore = TUFReplay.Shared.Database.Database;

namespace TUFReplay.Activity.Repositories;

public static class ActivityRepository
{
  private const string Select =
    @"SELECT l.id,l.level_id,l.app_session_id,g.tuf_level_id,l.opened_at_utc,l.closed_at_utc,g.level_tile_count,
count(r.id),coalesce(sum(CASE WHEN lower(r.result) IN ('cleared','completed') AND r.start_tile=0 AND r.no_fail_mode=0 THEN 1 ELSE 0 END),0),coalesce(sum(CASE WHEN r.no_fail_mode!=0 THEN 1 ELSE 0 END),0),min(r.start_tile),max(r.start_tile),
g.adofai_path,g.song,g.author,g.artist,g.metadata_state
FROM level_sessions l JOIN levels g ON g.id=l.level_id LEFT JOIN runs r ON r.level_session_id=l.id ";

  public static LevelSessionOverview GetLevelSessionOverview(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText = Select + " WHERE l.id=@id GROUP BY l.id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    return r.Read() ? Read(r) : null;
  }

  public static LogicalLevelOverview GetLogicalLevelOverview(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText =
      @"SELECT g.id,g.tuf_level_id,g.song,g.author,g.artist,g.first_seen_at_utc,g.last_seen_at_utc,
g.level_tile_count,count(DISTINCT l.id),count(r.id),
coalesce(sum(CASE WHEN lower(r.result) IN ('cleared','completed') AND r.start_tile=0 AND r.no_fail_mode=0 THEN 1 ELSE 0 END),0),
coalesce(sum(CASE WHEN r.no_fail_mode!=0 THEN 1 ELSE 0 END),0),min(r.start_tile),max(r.start_tile)
FROM levels g
LEFT JOIN level_sessions l ON l.level_id=g.id
LEFT JOIN runs r ON r.level_session_id=l.id
WHERE g.id=@id GROUP BY g.id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    if (!r.Read())
      return null;
    return new LogicalLevelOverview
    {
      Id = r.GetString(0),
      TufLevelId = DbValue.NullableInt(r, 1),
      Song = DbValue.NullableString(r, 2),
      Author = DbValue.NullableString(r, 3),
      Artist = DbValue.NullableString(r, 4),
      FirstSeenAtUtc = r.GetString(5),
      LastSeenAtUtc = r.GetString(6),
      LevelTileCount = r.GetInt32(7),
      VisitCount = r.GetInt32(8),
      RunCount = r.GetInt32(9),
      ClearRunCount = r.GetInt32(10),
      NoFailRunCount = r.GetInt32(11),
      FirstStartTile = DbValue.NullableInt(r, 12),
      LastStartTile = DbValue.NullableInt(r, 13),
      ChartAvailable = HasAvailableChart(id),
    };
  }

  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSessions(IReadOnlyList<string> appSessionIds)
  {
    var result = new List<LevelSessionOverview>();
    if (appSessionIds == null || appSessionIds.Count == 0)
      return result;

    using SqliteConnection c = DatabaseStore.OpenConnection();
    const int batchSize = 900;
    for (int batchStart = 0; batchStart < appSessionIds.Count; batchStart += batchSize)
    {
      int count = System.Math.Min(batchSize, appSessionIds.Count - batchStart);
      using SqliteCommand q = c.CreateCommand();
      var parameterNames = new string[count];
      for (int i = 0; i < count; i++)
      {
        string parameterName = "@app" + i;
        parameterNames[i] = parameterName;
        q.Parameters.AddWithValue(parameterName, appSessionIds[batchStart + i]);
      }

      q.CommandText =
        Select
        + " WHERE l.app_session_id IN ("
        + string.Join(",", parameterNames)
        + ") GROUP BY l.id ORDER BY l.app_session_id,l.opened_at_utc ASC";
      using SqliteDataReader r = q.ExecuteReader();
      while (r.Read())
        result.Add(Read(r));
    }
    return result;
  }

  private static LevelSessionOverview Read(SqliteDataReader r) =>
    new LevelSessionOverview
    {
      Id = r.GetString(0),
      LogicalLevelId = r.GetString(1),
      AppSessionId = r.GetString(2),
      TufLevelId = DbValue.NullableInt(r, 3),
      OpenedAtUtc = r.GetString(4),
      ClosedAtUtc = DbValue.NullableString(r, 5),
      LevelTileCount = r.GetInt32(6),
      RunCount = r.GetInt32(7),
      ClearRunCount = r.GetInt32(8),
      NoFailRunCount = r.GetInt32(9),
      FirstStartTile = DbValue.NullableInt(r, 10),
      LastStartTile = DbValue.NullableInt(r, 11),
      ChartAvailable = File.Exists(r.GetString(12)),
      LevelPath = r.GetString(12),
      Song = DbValue.NullableString(r, 13),
      Author = DbValue.NullableString(r, 14),
      Artist = DbValue.NullableString(r, 15),
      MetadataState = (LevelMetadataState)r.GetInt32(16),
    };

  private static bool HasAvailableChart(string id)
  {
    using SqliteConnection connection = DatabaseStore.OpenConnection();
    using SqliteCommand q = connection.CreateCommand();
    q.CommandText = "SELECT adofai_path FROM levels WHERE id=@id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    while (r.Read())
      if (File.Exists(r.GetString(0)))
        return true;
    return false;
  }
}
