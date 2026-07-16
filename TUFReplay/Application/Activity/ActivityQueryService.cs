using System.Collections.Generic;
using System.IO;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Adofai;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Activity;

public static class ActivityQueryService
{
  public static List<AppSessionActivity> ListAppSessionActivities(int offset, int limit)
  {
    List<AppSession> sessions = AppSessionRepository.List(offset, limit);
    var result = new List<AppSessionActivity>(sessions.Count);
    if (sessions.Count == 0)
      return result;

    var sessionIds = new List<string>(sessions.Count);
    foreach (AppSession session in sessions)
      sessionIds.Add(session.Id);

    List<LevelSessionOverview> levels = ActivityRepository.ListLevelSessionOverviewsByAppSessions(sessionIds);
    var levelsBySession = new Dictionary<string, List<LevelSessionOverview>>(sessions.Count);
    foreach (LevelSessionOverview level in levels)
    {
      EnsureLevelMetadata(level);
      if (!levelsBySession.TryGetValue(level.AppSessionId, out List<LevelSessionOverview> sessionLevels))
      {
        sessionLevels = new List<LevelSessionOverview>();
        levelsBySession.Add(level.AppSessionId, sessionLevels);
      }
      sessionLevels.Add(level);
    }

    foreach (AppSession session in sessions)
    {
      if (!levelsBySession.TryGetValue(session.Id, out List<LevelSessionOverview> sessionLevels))
        sessionLevels = new List<LevelSessionOverview>();
      result.Add(new AppSessionActivity { AppSession = session, LevelSessions = sessionLevels });
    }
    return result;
  }

  public static LevelSessionOverview GetLevelSessionOverview(string id)
  {
    LevelSessionOverview level = ActivityRepository.GetLevelSessionOverview(id);
    EnsureLevelMetadata(level);
    return level;
  }

  public static bool TryListRunsByLevelSession(string id, int offset, int limit, out List<RunRecord> runs)
  {
    runs = null;
    if (!LevelSessionRepository.Exists(id))
      return false;

    runs = RunRepository.ListByLevelSession(id, offset, limit);
    return true;
  }

  public static ChartData GetChart(string id)
  {
    LevelSession s = LevelSessionRepository.Get(id);
    if (s == null)
      return null;
    if (string.IsNullOrEmpty(s.LevelPath) || !File.Exists(s.LevelPath))
      return new ChartData { id = id, floorCount = s.LevelTileCount };
    return new ChartData
    {
      id = id,
      levelText = File.ReadAllText(s.LevelPath),
      floorCount = s.LevelTileCount,
    };
  }

  private static void EnsureLevelMetadata(LevelSessionOverview level)
  {
    if (level == null || level.MetadataState != LevelMetadataState.Pending)
      return;

    bool captured = AdofaiLevelMetadataReader.TryRead(level.LevelPath, out LevelMetadataSnapshot metadata);
    LevelMetadataState state = captured ? LevelMetadataState.Captured : LevelMetadataState.Unavailable;
    LevelSessionRepository.UpdateMetadata(level.Id, metadata, state);
    level.Song = metadata?.Song;
    level.Author = metadata?.Author;
    level.Artist = metadata?.Artist;
    level.MetadataState = state;
  }
}

public sealed class AppSessionActivity
{
  public AppSession AppSession;
  public List<LevelSessionOverview> LevelSessions;
}

public sealed class ChartData
{
  public string id;
  public string levelText;
  public int floorCount;
}
