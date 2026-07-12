using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Domain.Activity;
using DatabaseStore = TUFReplay.Infrastructure.Database.Database;
namespace TUFReplay.Infrastructure.Database.Repositories;
public static class ActivityRepository
{
  private const string Select = @"SELECT l.id,l.app_session_id,l.tuf_level_id,l.opened_at_utc,l.closed_at_utc,l.level_tile_count,
count(r.id),coalesce(sum(CASE WHEN r.result='cleared' THEN 1 ELSE 0 END),0),coalesce(sum(CASE WHEN r.no_fail_mode!=0 THEN 1 ELSE 0 END),0),min(r.start_tile),max(r.start_tile),
l.level_path FROM level_sessions l LEFT JOIN runs r ON r.level_session_id=l.id ";
  public static LevelSessionOverview GetLevelSessionOverview(string id) { using SqliteConnection c=DatabaseStore.OpenConnection();using SqliteCommand q=c.CreateCommand();q.CommandText=Select+" WHERE l.id=@id GROUP BY l.id";q.Parameters.AddWithValue("@id",id);using SqliteDataReader r=q.ExecuteReader();return r.Read()?Read(r):null; }
  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSession(string id) { var result=new List<LevelSessionOverview>();using SqliteConnection c=DatabaseStore.OpenConnection();using SqliteCommand q=c.CreateCommand();q.CommandText=Select+" WHERE l.app_session_id=@id GROUP BY l.id ORDER BY l.opened_at_utc ASC";q.Parameters.AddWithValue("@id",id);using SqliteDataReader r=q.ExecuteReader();while(r.Read())result.Add(Read(r));return result; }
  private static LevelSessionOverview Read(SqliteDataReader r)=>new LevelSessionOverview{Id=r.GetString(0),AppSessionId=r.GetString(1),TufLevelId=DbValue.NullableInt(r,2),OpenedAtUtc=r.GetString(3),ClosedAtUtc=DbValue.NullableString(r,4),LevelTileCount=r.GetInt32(5),RunCount=r.GetInt32(6),ClearRunCount=r.GetInt32(7),NoFailRunCount=r.GetInt32(8),FirstStartTile=DbValue.NullableInt(r,9),LastStartTile=DbValue.NullableInt(r,10),ChartAvailable=File.Exists(r.GetString(11))};
}
