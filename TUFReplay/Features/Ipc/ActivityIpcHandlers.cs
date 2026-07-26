using System;
using System.Collections.Generic;
using AdofaiIpc.Core;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Replay;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Activity;
using TUFReplay.Infrastructure.Database.Repositories;
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

  public static object GetLogicalLevel(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLogicalLevelId();
    LogicalLevelOverview level = ActivityQueryService.GetLogicalLevelOverview(id);
    return level == null
      ? IpcDomainError.Create("logical_level_not_found", "Logical level was not found.")
      : ActivityLogicalLevelOverviewDto.From(level);
  }

  public static object ListLogicalLevelRuns(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLogicalLevelId();
    if (!IpcParams.TryRequiredStringArray(request, "appSessionIds", out List<string> appSessionIds))
      return IpcDomainError.Create("invalid_app_session_ids", "appSessionIds must be a non-empty string array.");
    IpcPagination pagination = IpcPagination.Parse(request);
    if (
      !ActivityQueryService.TryListRunsByLogicalLevel(
        id,
        appSessionIds,
        pagination.Offset,
        pagination.Limit,
        out List<RunRecord> runs
      )
    )
      return IpcDomainError.Create("logical_level_not_found", "Logical level was not found.");
    var output = new List<ActivityRunDto>(runs.Count);
    foreach (RunRecord run in runs)
      output.Add(ActivityRunDto.From(run));
    return output;
  }

  public static object GetLogicalLevelChart(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "id", out string id))
      return InvalidLogicalLevelId();
    try
    {
      ChartData chart = ActivityQueryService.GetLogicalLevelChart(id);
      if (chart == null)
        return IpcDomainError.Create("logical_level_not_found", "Logical level was not found.");
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
      Main.Instance?.Log("[IPC] Logical level chart read failed: " + exception.GetType().Name);
      return IpcDomainError.Create("chart_read_failed", "The recorded chart could not be read.");
    }
  }

  public static object DeleteRun(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");

    ReplayPlaybackStatus replayStatus = ReplayPlaybackCoordinator.GetStatus();
    if (ReplayPlaybackCoordinator.IsBusy && replayStatus.RunId == runId)
      return IpcDomainError.Create("run_in_use", "Stop this replay before deleting its run.");

    FeatureRegistry.MicrophoneRecording?.BeginRunDeletion(runId);
    try
    {
      if (!RunRepository.Delete(runId))
      {
        FeatureRegistry.MicrophoneRecording?.CancelRunDeletion(runId);
        return IpcDomainError.Create("run_not_found", "Run was not found.");
      }

      FeatureRegistry.MicrophoneRecording?.CompleteRunDeletion(runId);
      return new ActivityRunDeleteResultDto { RunId = runId, Deleted = true };
    }
    catch (Exception exception)
    {
      FeatureRegistry.MicrophoneRecording?.CancelRunDeletion(runId);
      Main.Instance?.Log("[IPC] Run deletion failed: " + exception.GetType().Name);
      return IpcDomainError.Create("run_delete_failed", "The run could not be deleted.");
    }
  }

  private static object InvalidLevelSessionId() =>
    IpcDomainError.Create("invalid_level_session_id", "id must be a non-empty string.");

  private static object InvalidLogicalLevelId() =>
    IpcDomainError.Create("invalid_logical_level_id", "id must be a non-empty string.");
}
