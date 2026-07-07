using System.Collections.Generic;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Activity;

public static class ActivityQueryService
{
  public static List<ActivityDaySummary> ListDaySummaries(string fromUtc, string toUtc)
  {
    return ActivityRepository.ListDaySummaries(fromUtc, toUtc);
  }

  public static List<AppSession> ListAppSessions(string fromUtc, string toUtc)
  {
    return AppSessionRepository.List(fromUtc, toUtc);
  }

  public static AppSession GetAppSession(string id)
  {
    return AppSessionRepository.Get(id);
  }

  public static List<LevelSessionOverview> ListLevelSessionOverviewsByAppSession(string appSessionId)
  {
    return ActivityRepository.ListLevelSessionOverviewsByAppSession(appSessionId);
  }

  public static LevelSessionOverview GetLevelSessionOverview(string id)
  {
    return ActivityRepository.GetLevelSessionOverview(id);
  }

  public static List<SegmentGroupSummary> ListSegmentGroups(string levelSessionId)
  {
    return RunRepository.ListSegmentGroups(levelSessionId);
  }

  public static List<RunRecord> ListRunsByLevelSession(string levelSessionId, int limit, int offset)
  {
    return RunRepository.ListByLevelSession(levelSessionId, limit, offset);
  }

  public static List<RunRecord> ListRunsBySegmentGroup(string levelSessionId, int segmentGroupIndex)
  {
    return RunRepository.ListBySegmentGroup(levelSessionId, segmentGroupIndex);
  }
}
