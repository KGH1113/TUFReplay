using System;
using System.Collections.Generic;
using System.Globalization;
using AdofaiIpc.Core;
using TUFReplay.Application.Activity;
using TUFReplay.Domain.Activity;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class TUFReplayIpcHandlers
{
  public static object Health(IpcRequest request)
  {
    return HealthResponseDto.Create();
  }

  public static object ListActivityDays(IpcRequest request)
  {
    string fromUtc = IpcParams.OptionalString(request, "from");
    string toUtc = IpcParams.OptionalString(request, "to");
    return MapDaySummaries(ActivityQueryService.ListDaySummaries(fromUtc, toUtc));
  }

  public static object GetActivityDay(IpcRequest request)
  {
    string date = IpcParams.OptionalString(request, "date");
    if (!TryGetUtcDayRange(date, out string fromUtc, out string toUtc))
      return IpcDomainError.Create("invalid_date");

    List<ActivityDaySummary> summaries = ActivityQueryService.ListDaySummaries(fromUtc, toUtc);
    ActivityDaySummaryDto summary = summaries.Count > 0
      ? ActivityDaySummaryDto.From(summaries[0])
      : EmptyDaySummary(date);

    List<ActivityAppSessionDto> appSessionDtos = new List<ActivityAppSessionDto>();
    List<AppSession> appSessions = ActivityQueryService.ListAppSessions(fromUtc, toUtc);
    appSessions.Reverse();

    foreach (AppSession appSession in appSessions)
    {
      List<ActivityLevelSessionOverviewDto> levelDtos = new List<ActivityLevelSessionOverviewDto>();
      List<LevelSessionOverview> levels =
        ActivityQueryService.ListLevelSessionOverviewsByAppSession(appSession.Id);

      foreach (LevelSessionOverview level in levels)
        levelDtos.Add(ActivityLevelSessionOverviewDto.From(level));

      appSessionDtos.Add(ActivityAppSessionDto.From(appSession, levelDtos));
    }

    return new ActivityDayDto
    {
      Date = date,
      Summary = summary,
      AppSessions = appSessionDtos
    };
  }

  public static object GetActivityAppSession(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    AppSession session = ActivityQueryService.GetAppSession(id);
    if (session == null) return IpcDomainError.Create("app_session_not_found");

    List<ActivityLevelSessionOverviewDto> levelDtos = new List<ActivityLevelSessionOverviewDto>();
    List<LevelSessionOverview> levels = ActivityQueryService.ListLevelSessionOverviewsByAppSession(id);

    foreach (LevelSessionOverview level in levels)
      levelDtos.Add(ActivityLevelSessionOverviewDto.From(level));

    return ActivityAppSessionDto.From(session, levelDtos);
  }

  public static object GetActivityLevelSession(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    LevelSessionOverview session = ActivityQueryService.GetLevelSessionOverview(id);
    if (session == null) return IpcDomainError.Create("level_session_not_found");

    return new ActivityLevelSessionDto
    {
      Session = ActivityLevelSessionOverviewDto.From(session),
      SegmentGroups = MapSegmentGroups(ActivityQueryService.ListSegmentGroups(id))
    };
  }

  public static object ListActivitySegments(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return IpcDomainError.Create("level_session_not_found");

    return MapSegmentGroups(ActivityQueryService.ListSegmentGroups(id));
  }

  public static object ListActivityRuns(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    int? offset = IpcParams.OptionalInt(request, "offset");
    int? limit = IpcParams.OptionalInt(request, "limit");

    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return IpcDomainError.Create("level_session_not_found");

    return MapRuns(ActivityQueryService.ListRunsByLevelSession(
      id,
      NormalizeOffset(offset),
      NormalizeLimit(limit)
    ));
  }

  public static object ListActivitySegmentRuns(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    int segmentGroupIndex = IpcParams.OptionalInt(request, "segmentGroupIndex") ?? 0;

    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return IpcDomainError.Create("level_session_not_found");

    return MapRuns(ActivityQueryService.ListRunsBySegmentGroup(id, segmentGroupIndex));
  }

  private static List<ActivityDaySummaryDto> MapDaySummaries(List<ActivityDaySummary> summaries)
  {
    List<ActivityDaySummaryDto> dtos = new List<ActivityDaySummaryDto>();
    foreach (ActivityDaySummary summary in summaries) dtos.Add(ActivityDaySummaryDto.From(summary));
    return dtos;
  }

  private static List<ActivitySegmentGroupDto> MapSegmentGroups(List<SegmentGroupSummary> groups)
  {
    List<ActivitySegmentGroupDto> dtos = new List<ActivitySegmentGroupDto>();
    foreach (SegmentGroupSummary group in groups) dtos.Add(ActivitySegmentGroupDto.From(group));
    return dtos;
  }

  private static List<ActivityRunDto> MapRuns(List<RunRecord> runs)
  {
    List<ActivityRunDto> dtos = new List<ActivityRunDto>();
    foreach (RunRecord run in runs) dtos.Add(ActivityRunDto.From(run));
    return dtos;
  }

  private static bool TryGetUtcDayRange(string date, out string fromUtc, out string toUtc)
  {
    fromUtc = null;
    toUtc = null;

    if (!DateTime.TryParseExact(
      date,
      "yyyy-MM-dd",
      CultureInfo.InvariantCulture,
      DateTimeStyles.None,
      out DateTime day
    ))
    {
      return false;
    }

    DateTime utcDay = DateTime.SpecifyKind(day, DateTimeKind.Utc);
    fromUtc = utcDay.ToString("o");
    toUtc = utcDay.AddDays(1).ToString("o");
    return true;
  }

  private static int NormalizeOffset(int? offset)
  {
    if (!offset.HasValue || offset.Value < 0) return 0;
    return offset.Value;
  }

  private static int NormalizeLimit(int? limit)
  {
    if (!limit.HasValue || limit.Value <= 0) return 200;
    return Math.Min(limit.Value, 1000);
  }

  private static ActivityDaySummaryDto EmptyDaySummary(string date)
  {
    return new ActivityDaySummaryDto
    {
      Date = date,
      AppSessionCount = 0,
      LevelSessionCount = 0,
      RunCount = 0,
      NoFailRunCount = 0,
      UniqueLevelCount = 0,
      StartedAtUtc = null,
      EndedAtUtc = null
    };
  }
}
