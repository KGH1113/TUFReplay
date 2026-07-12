using System.Collections.Generic;
using System.IO;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;
namespace TUFReplay.Application.Activity;
public static class ActivityQueryService
{
  public static List<AppSession> ListAppSessions(int offset,int limit)=>AppSessionRepository.List(offset,limit);
  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSession(string id)=>ActivityRepository.ListLevelSessionOverviewsByAppSession(id);
  public static LevelSessionOverview GetLevelSessionOverview(string id)=>ActivityRepository.GetLevelSessionOverview(id);
  public static List<RunRecord> ListRunsByLevelSession(string id,int offset,int limit)=>RunRepository.ListByLevelSession(id,offset,limit);
  public static ChartData GetChart(string id) { LevelSession s=LevelSessionRepository.Get(id); if(s==null)return null; if(string.IsNullOrEmpty(s.LevelPath)||!File.Exists(s.LevelPath))return new ChartData{id=id,floorCount=s.LevelTileCount}; return new ChartData{id=id,levelText=File.ReadAllText(s.LevelPath),floorCount=s.LevelTileCount}; }
}
public sealed class ChartData { public string id; public string levelText; public int floorCount; }
