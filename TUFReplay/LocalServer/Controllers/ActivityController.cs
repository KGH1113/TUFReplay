using System;
using System.Collections.Generic;
using System.Globalization;
using TUFReplay.Application.Activity;
using TUFReplay.Domain.Activity;
using TUFReplay.LocalServer.Binding;
using TUFReplay.LocalServer.Dtos;
using TUFReplay.LocalServer.Http;
using TUFReplay.LocalServer.Routing;

namespace TUFReplay.LocalServer.Controllers;

[Controller("/api/activity")]
public sealed class ActivityController
{
  [Get("/days")]
  public object ListDays([Query("from")] string fromUtc, [Query("to")] string toUtc)
  {
    return MapDaySummaries(ActivityQueryService.ListDaySummaries(fromUtc, toUtc));
  }

  [Get("/days/:date")]
  public ServerResponse Day([Param("date")] string date)
  {
    if (!TryGetUtcDayRange(date, out string fromUtc, out string toUtc))
      return ServerResponse.BadRequest(new { error = "invalid_date" });

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

    return ServerResponse.Ok(new ActivityDayDto
    {
      Date = date,
      Summary = summary,
      AppSessions = appSessionDtos
    });
  }

  [Get("/app-sessions/:id")]
  public ServerResponse AppSession([Param("id")] string id)
  {
    AppSession session = ActivityQueryService.GetAppSession(id);
    if (session == null) return ServerResponse.NotFound(new { error = "app_session_not_found" });

    List<ActivityLevelSessionOverviewDto> levelDtos = new List<ActivityLevelSessionOverviewDto>();
    List<LevelSessionOverview> levels = ActivityQueryService.ListLevelSessionOverviewsByAppSession(id);

    foreach (LevelSessionOverview level in levels)
      levelDtos.Add(ActivityLevelSessionOverviewDto.From(level));

    return ServerResponse.Ok(ActivityAppSessionDto.From(session, levelDtos));
  }

  [Get("/level-sessions/:id")]
  public ServerResponse LevelSession([Param("id")] string id)
  {
    LevelSessionOverview session = ActivityQueryService.GetLevelSessionOverview(id);
    if (session == null) return ServerResponse.NotFound(new { error = "level_session_not_found" });

    return ServerResponse.Ok(new ActivityLevelSessionDto
    {
      Session = ActivityLevelSessionOverviewDto.From(session),
      SegmentGroups = MapSegmentGroups(ActivityQueryService.ListSegmentGroups(id))
    });
  }

  [Get("/level-sessions/:id/segments")]
  public ServerResponse Segments([Param("id")] string id)
  {
    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return ServerResponse.NotFound(new { error = "level_session_not_found" });

    return ServerResponse.Ok(MapSegmentGroups(ActivityQueryService.ListSegmentGroups(id)));
  }

  [Get("/level-sessions/:id/runs")]
  public ServerResponse Runs(
    [Param("id")] string id,
    [Query("offset")] int? offset,
    [Query("limit")] int? limit
  )
  {
    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return ServerResponse.NotFound(new { error = "level_session_not_found" });

    return ServerResponse.Ok(MapRuns(ActivityQueryService.ListRunsByLevelSession(
      id,
      NormalizeOffset(offset),
      NormalizeLimit(limit)
    )));
  }

  [Get("/level-sessions/:id/segments/:segmentGroupIndex/runs")]
  public ServerResponse SegmentRuns(
    [Param("id")] string id,
    [Param("segmentGroupIndex")] int segmentGroupIndex
  )
  {
    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return ServerResponse.NotFound(new { error = "level_session_not_found" });

    return ServerResponse.Ok(MapRuns(ActivityQueryService.ListRunsBySegmentGroup(id, segmentGroupIndex)));
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
