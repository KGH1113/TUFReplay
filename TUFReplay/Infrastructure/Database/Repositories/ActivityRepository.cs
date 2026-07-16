using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;

namespace TUFReplay.Infrastructure.Database.Repositories;

public static class ActivityRepository
{
  private const string Select =
    @"SELECT l.id,l.app_session_id,l.tuf_level_id,l.opened_at_utc,l.closed_at_utc,l.level_tile_count,
count(r.id),coalesce(sum(CASE WHEN r.result='cleared' THEN 1 ELSE 0 END),0),coalesce(sum(CASE WHEN r.no_fail_mode!=0 THEN 1 ELSE 0 END),0),min(r.start_tile),max(r.start_tile),
l.level_path,l.song,l.author,l.artist,l.metadata_state
FROM level_sessions l LEFT JOIN runs r ON r.level_session_id=l.id ";

  public static LevelSessionOverview GetLevelSessionOverview(string id)
  {
    using SqliteConnection c = DatabaseStore.OpenConnection();
    using SqliteCommand q = c.CreateCommand();
    q.CommandText = Select + " WHERE l.id=@id GROUP BY l.id";
    q.Parameters.AddWithValue("@id", id);
    using SqliteDataReader r = q.ExecuteReader();
    return r.Read() ? Read(r) : null;
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
      AppSessionId = r.GetString(1),
      TufLevelId = DbValue.NullableInt(r, 2),
      OpenedAtUtc = r.GetString(3),
      ClosedAtUtc = DbValue.NullableString(r, 4),
      LevelTileCount = r.GetInt32(5),
      RunCount = r.GetInt32(6),
      ClearRunCount = r.GetInt32(7),
      NoFailRunCount = r.GetInt32(8),
      FirstStartTile = DbValue.NullableInt(r, 9),
      LastStartTile = DbValue.NullableInt(r, 10),
      ChartAvailable = File.Exists(r.GetString(11)),
      LevelPath = r.GetString(11),
      Song = DbValue.NullableString(r, 12),
      Author = DbValue.NullableString(r, 13),
      Artist = DbValue.NullableString(r, 14),
      MetadataState = (LevelMetadataState)r.GetInt32(15),
    };
}
