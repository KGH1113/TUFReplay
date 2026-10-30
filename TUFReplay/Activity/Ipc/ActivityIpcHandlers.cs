using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using TUFReplay.Activity.Ipc;
using TUFReplay.Activity.Management;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Queries;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Database;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Activity.Ipc;

public static class ActivityIpcHandlers
{
  public static List<ActivityAppSessionDto> ListAppSessions(int? offset = null, int? limit = null)
  {
    IpcPagination pagination = IpcPagination.Parse(offset, limit);
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

  public static ActivityLegacyReplayStatusDto GetLegacyReplayStatus() =>
    new ActivityLegacyReplayStatusDto { HasLegacyReplays = RunRepository.HasLegacyReplay() };

  public static ActivityLevelSessionOverviewDto GetLevelSession(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLevelSessionId();

    LevelSessionOverview session = ActivityQueryService.GetLevelSessionOverview(id);
    return session == null
      ? throw IpcDomainError.Create("level_session_not_found", "Level session was not found.")
      : ActivityLevelSessionOverviewDto.From(session);
  }

  public static List<ActivityRunDto> ListRuns(string id, int? offset = null, int? limit = null)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLevelSessionId();

    IpcPagination pagination = IpcPagination.Parse(offset, limit);
    if (
      !ActivityQueryService.TryListRunsByLevelSession(id, pagination.Offset, pagination.Limit, out List<RunRecord> runs)
    )
      throw IpcDomainError.Create("level_session_not_found", "Level session was not found.");

    var output = new List<ActivityRunDto>(runs.Count);
    foreach (RunRecord run in runs)
      output.Add(ActivityRunDto.From(run));
    return output;
  }

  public static ActivityChartDto GetChart(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLevelSessionId();

    try
    {
      ChartData chart = ActivityQueryService.GetChart(id);
      if (chart == null)
        throw IpcDomainError.Create("level_session_not_found", "Level session was not found.");
      if (chart.levelText == null)
        throw IpcDomainError.Create(
          chart.errorCode ?? "chart_unavailable",
          chart.errorMessage ?? "The recorded chart file is unavailable."
        );
      return new ActivityChartDto
      {
        LevelSessionId = chart.id,
        LevelText = chart.levelText,
        FloorCount = chart.floorCount,
      };
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Chart read failed: " + exception.GetType().Name);
      throw ChartReadError(exception);
    }
  }

  public static ActivityLogicalLevelOverviewDto GetLogicalLevel(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLogicalLevelId();
    LogicalLevelOverview level = ActivityQueryService.GetLogicalLevelOverview(id);
    return level == null
      ? throw IpcDomainError.Create("logical_level_not_found", "Logical level was not found.")
      : ActivityLogicalLevelOverviewDto.From(level);
  }

  public static List<ActivityRunDto> ListLogicalLevelRuns(
    string id,
    string[] appSessionIds,
    int? offset = null,
    int? limit = null
  )
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLogicalLevelId();
    if (
      appSessionIds == null
      || appSessionIds.Length == 0
      || System.Array.Exists(appSessionIds, string.IsNullOrWhiteSpace)
    )
      throw IpcDomainError.Create("invalid_app_session_ids", "appSessionIds must be a non-empty string array.");
    IpcPagination pagination = IpcPagination.Parse(offset, limit);
    if (
      !ActivityQueryService.TryListRunsByLogicalLevel(
        id,
        new List<string>(appSessionIds),
        pagination.Offset,
        pagination.Limit,
        out List<RunRecord> runs
      )
    )
      throw IpcDomainError.Create("logical_level_not_found", "Logical level was not found.");
    var output = new List<ActivityRunDto>(runs.Count);
    foreach (RunRecord run in runs)
      output.Add(ActivityRunDto.From(run));
    return output;
  }

  public static ActivityChartDto GetLogicalLevelChart(string id)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw InvalidLogicalLevelId();
    try
    {
      ChartData chart = ActivityQueryService.GetLogicalLevelChart(id);
      if (chart == null)
        throw IpcDomainError.Create("logical_level_not_found", "Logical level was not found.");
      if (chart.levelText == null)
        throw IpcDomainError.Create(
          chart.errorCode ?? "chart_unavailable",
          chart.errorMessage ?? "The recorded chart file is unavailable."
        );
      return new ActivityChartDto
      {
        LevelSessionId = chart.id,
        LevelText = chart.levelText,
        FloorCount = chart.floorCount,
      };
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Logical level chart read failed: " + exception.GetType().Name);
      throw ChartReadError(exception);
    }
  }

  public static ActivityRunDeleteResultDto DeleteRun(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    if (RunRepository.IsLocked(runId))
      throw RunLocked();
    ReplayPlaybackStatus replayStatus = ReplayPlaybackCoordinator.GetStatus();
    if (ReplayPlaybackCoordinator.IsBusy && replayStatus.RunId == runId)
      throw IpcDomainError.Create("run_in_use", "Stop this replay before deleting its run.");

    FeatureRegistry.MicrophoneRecording?.BeginRunDeletion(runId);
    try
    {
      if (!RunRepository.Delete(runId))
      {
        FeatureRegistry.MicrophoneRecording?.CancelRunDeletion(runId);
        throw RunRepository.IsLocked(runId)
          ? RunLocked()
          : IpcDomainError.Create("run_not_found", "Run was not found.");
      }

      FeatureRegistry.MicrophoneRecording?.CompleteRunDeletion(runId);
      FeatureRegistry.WebcamRecording?.Store?.DeleteRun(runId);
      ActivityChanges.Notify(runId);
      return new ActivityRunDeleteResultDto { RunId = runId, Deleted = true };
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      FeatureRegistry.MicrophoneRecording?.CancelRunDeletion(runId);
      Main.Instance?.Log("[IPC] Run deletion failed: " + exception.GetType().Name);
      if (exception is SqliteException sqliteException && Database.IsTransientLock(sqliteException))
      {
        throw IpcDomainError.Create(
          "activity_busy",
          "Play history is being updated. Wait a moment and try deleting the run again."
        );
      }
      throw IpcDomainError.Create("run_delete_failed", "The run could not be deleted. Try again shortly.");
    }
  }

  public static ActivityRunLockResultDto ChangeRunLock(string runId, bool locked)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    try
    {
      if (!RunLocks.Change(runId, locked))
        throw IpcDomainError.Create("run_not_found", "Run was not found.");
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Run lock change failed: " + exception.GetType().Name);
      if (exception is SqliteException sqliteException && Database.IsTransientLock(sqliteException))
        throw IpcDomainError.Create("activity_busy", "Play history is being updated. Try again in a moment.");
      throw IpcDomainError.Create("run_lock_failed", "The lock could not be changed. Try again shortly.");
    }
    ActivityChanges.Notify(runId);
    return new ActivityRunLockResultDto { RunId = runId, Locked = locked };
  }

  // Runs on a worker thread after gameplay is idle (see TUFReplayIpcFeature).
  public static ActivityRunBatchDeleteResultDto DeleteAppSessionRuns(string[] appSessionIds, bool keepClears)
  {
    if (
      appSessionIds == null
      || appSessionIds.Length == 0
      || System.Array.Exists(appSessionIds, string.IsNullOrWhiteSpace)
    )
      throw IpcDomainError.Create("invalid_app_session_ids", "appSessionIds must be a non-empty string array.");
    try
    {
      string replayRunId = ReplayPlaybackCoordinator.IsBusy ? ReplayPlaybackCoordinator.GetStatus().RunId : null;
      RunBatchDeletion result = AppSessionRunDeletion.Delete(appSessionIds, keepClears, replayRunId);
      if (result.DeletedRunCount > 0 || result.NewlyLockedRunCount > 0)
        ActivityChanges.Notify();
      return new ActivityRunBatchDeleteResultDto
      {
        DeletedRunCount = result.DeletedRunCount,
        KeptLockedRunCount = result.KeptLockedRunCount,
        KeptInUseRunCount = result.KeptInUseRunCount,
      };
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Day run deletion failed: " + exception.GetType().Name);
      if (exception is SqliteException sqliteException && Database.IsTransientLock(sqliteException))
      {
        throw IpcDomainError.Create(
          "activity_busy",
          "Play history is being updated. Wait a moment and try deleting the day again."
        );
      }
      throw IpcDomainError.Create("day_delete_failed", "The runs could not be deleted. Try again shortly.");
    }
  }

  private static AdofaiIpc.IpcCallException RunLocked() =>
    IpcDomainError.Create("run_locked", "Unlock this run to delete it.");

  private static AdofaiIpc.IpcCallException InvalidLevelSessionId() =>
    IpcDomainError.Create("invalid_level_session_id", "id must be a non-empty string.");

  private static AdofaiIpc.IpcCallException InvalidLogicalLevelId() =>
    IpcDomainError.Create("invalid_logical_level_id", "id must be a non-empty string.");

  private static AdofaiIpc.IpcCallException ChartReadError(Exception exception)
  {
    if (exception is SqliteException sqliteException && Database.IsTransientLock(sqliteException))
      return IpcDomainError.Create("activity_busy", "Play history is being updated. Try again shortly.");
    if (exception is UnauthorizedAccessException)
    {
      return IpcDomainError.Create(
        "chart_permission_denied",
        "TUFReplay does not have permission to read the level file."
      );
    }
    if (exception is IOException)
      return IpcDomainError.Create("chart_read_failed", "The level file could not be read. Try again shortly.");
    return IpcDomainError.Create("chart_read_failed", "The chart could not be loaded. Try again shortly.");
  }
}
