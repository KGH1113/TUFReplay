using System.Collections.Generic;
using TUFReplay.Activity.Charts;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;

namespace TUFReplay.Activity.Queries;

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
      EnsureTufLevelId(level);
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
    EnsureTufLevelId(level);
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

  public static LogicalLevelOverview GetLogicalLevelOverview(string id) =>
    ActivityRepository.GetLogicalLevelOverview(id);

  public static bool TryListRunsByLogicalLevel(
    string id,
    List<string> appSessionIds,
    int offset,
    int limit,
    out List<RunRecord> runs
  )
  {
    runs = null;
    if (!LevelRepository.Exists(id))
      return false;
    runs = RunRepository.ListByLogicalLevel(id, appSessionIds, offset, limit);
    return true;
  }

  public static ChartData GetLogicalLevelChart(string id)
  {
    if (!LevelRepository.Exists(id))
      return null;
    List<LevelSession> sessions = LevelSessionRepository.ListByLogicalLevelNewestFirst(id);
    int floorCount = sessions.Count == 0 ? 0 : sessions[0].LevelTileCount;
    foreach (LevelSession session in sessions)
    {
      floorCount = System.Math.Max(floorCount, session.LevelTileCount);
      if (
        !LevelFileAccessValidator.TryValidate(
          session,
          out _,
          out string levelText,
          out string errorCode,
          out string errorMessage
        )
      )
        return new ChartData
        {
          id = id,
          floorCount = floorCount,
          errorCode = errorCode,
          errorMessage = errorMessage,
        };
      return new ChartData
      {
        id = id,
        levelText = levelText,
        floorCount = session.LevelTileCount,
      };
    }
    return new ChartData { id = id, floorCount = floorCount };
  }

  public static ChartData GetChart(string id)
  {
    LevelSession s = LevelSessionRepository.Get(id);
    if (s == null)
      return null;
    if (
      !LevelFileAccessValidator.TryValidate(
        s,
        out _,
        out string levelText,
        out string errorCode,
        out string errorMessage
      )
    )
      return new ChartData
      {
        id = id,
        floorCount = s.LevelTileCount,
        errorCode = errorCode,
        errorMessage = errorMessage,
      };
    return new ChartData
    {
      id = id,
      levelText = levelText,
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

  private static void EnsureTufLevelId(LevelSessionOverview level)
  {
    if (level == null || level.TufLevelId.HasValue)
      return;

    int? tufLevelId = TufHelperGateway.ResolveTufLevelId(level.LevelPath);
    if (!tufLevelId.HasValue)
      return;

    level.LogicalLevelId = LevelSessionRepository.UpdateTufLevelIdIfMissing(level.Id, tufLevelId.Value);
    level.TufLevelId = tufLevelId;
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
  public string errorCode;
  public string errorMessage;
}
