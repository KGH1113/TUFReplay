using System;
using System.Collections.Generic;
using AdofaiIpc.Core;
using TUFReplay.Application.Activity;
using TUFReplay.Domain.Activity;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class ActivityIpcHandlers
{
  public static object ListAppSessions(IpcRequest request)
  {
    IpcPagination pagination = IpcPagination.Parse(request);
    var output = new List<ActivityAppSessionDto>();
    foreach (
      AppSessionActivity activity in ActivityQueryService.ListAppSessionActivities(pagination.Offset, pagination.Limit)
    )
    {
      var levels = new List<ActivityLevelSessionOverviewDto>(activity.LevelSessions.Count);
      foreach (LevelSessionOverview level in activity.LevelSessions)
        levels.Add(ActivityLevelSessionOverviewDto.From(level));
      output.Add(ActivityAppSessionDto.From(activity.AppSession, levels));
    }
    return output;
  }

  public static object GetLevelSession(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLevelSessionId();

    LevelSessionOverview session = ActivityQueryService.GetLevelSessionOverview(id);
    return session == null
      ? IpcDomainError.Create("level_session_not_found", "Level session was not found.")
      : ActivityLevelSessionOverviewDto.From(session);
  }

  public static object ListRuns(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLevelSessionId();

    IpcPagination pagination = IpcPagination.Parse(request);
    if (
      !ActivityQueryService.TryListRunsByLevelSession(id, pagination.Offset, pagination.Limit, out List<RunRecord> runs)
    )
      return IpcDomainError.Create("level_session_not_found", "Level session was not found.");

    var output = new List<ActivityRunDto>(runs.Count);
    foreach (RunRecord run in runs)
      output.Add(ActivityRunDto.From(run));
    return output;
  }

  public static object GetChart(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLevelSessionId();

    try
    {
      ChartData chart = ActivityQueryService.GetChart(id);
      if (chart == null)
        return IpcDomainError.Create("level_session_not_found", "Level session was not found.");
      if (chart.levelText == null)
        return IpcDomainError.Create("chart_unavailable", "The recorded chart file is unavailable or has changed.");
      return new ActivityChartDto
      {
        LevelSessionId = chart.id,
        LevelText = chart.levelText,
        FloorCount = chart.floorCount,
      };
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[IPC] Chart read failed: " + exception.GetType().Name);
      return IpcDomainError.Create("chart_read_failed", "The recorded chart could not be read.");
    }
  }

  private static object InvalidLevelSessionId() =>
    IpcDomainError.Create("invalid_level_session_id", "id must be a non-empty string.");
}
