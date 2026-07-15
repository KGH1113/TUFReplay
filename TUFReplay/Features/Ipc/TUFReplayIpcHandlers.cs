using System;
using System.Collections.Generic;
using AdofaiIpc.Core;
using TUFReplay.Application.Activity;
using TUFReplay.Application.Replay;
using TUFReplay.Domain.Activity;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class TUFReplayIpcHandlers
{
  public static object Health(IpcRequest request) => HealthResponseDto.Create();

  public static object ListAppSessions(IpcRequest request)
  {
    int offset = Offset(request),
      limit = Limit(request);
    var output = new List<ActivityAppSessionDto>();
    foreach (AppSession s in ActivityQueryService.ListAppSessions(offset, limit))
    {
      var levels = new List<ActivityLevelSessionOverviewDto>();
      foreach (var l in ActivityQueryService.ListLevelSessionOverviewsByAppSession(s.Id))
        levels.Add(ActivityLevelSessionOverviewDto.From(l));
      output.Add(ActivityAppSessionDto.From(s, levels));
    }
    return output;
  }

  public static object GetLevelSession(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    var s = ActivityQueryService.GetLevelSessionOverview(id);
    return s == null
      ? IpcDomainError.Create("level_session_not_found", "Level session was not found.")
      : ActivityLevelSessionOverviewDto.From(s);
  }

  public static object ListRuns(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    if (ActivityQueryService.GetLevelSessionOverview(id) == null)
      return IpcDomainError.Create("level_session_not_found", "Level session was not found.");
    var output = new List<ActivityRunDto>();
    foreach (var r in ActivityQueryService.ListRunsByLevelSession(id, Offset(request), Limit(request)))
      output.Add(ActivityRunDto.From(r));
    return output;
  }

  public static object GetChart(IpcRequest request)
  {
    string id = IpcParams.OptionalString(request, "id");
    try
    {
      ChartData x = ActivityQueryService.GetChart(id);
      if (x == null)
        return IpcDomainError.Create("level_session_not_found", "Level session was not found.");
      if (x.levelText == null)
        return IpcDomainError.Create("chart_unavailable", "The recorded chart path is unavailable.");
      return new ActivityChartDto
      {
        LevelSessionId = x.id,
        LevelText = x.levelText,
        FloorCount = x.floorCount,
      };
    }
    catch (Exception ex)
    {
      Main.Instance?.Log("[IPC] Chart read failed: " + ex.GetType().Name);
      return IpcDomainError.Create("chart_read_failed", "The recorded chart could not be read.");
    }
  }

  public static object PlayReplay(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    string levelPath = IpcParams.OptionalString(request, "levelPath");
    return ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.Play(runId, levelPath));
  }

  public static object GetReplayStatus(IpcRequest request)
  {
    return ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus());
  }

  public static object StartReplayLevelFilePicker(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    return ReplayLevelFilePickerCoordinator.TryStart(
      runId,
      out ReplayLevelFilePickerStatus status,
      out string errorCode,
      out string errorMessage
    )
      ? ReplayLevelFilePickerStatusDto.From(status)
      : IpcDomainError.Create(errorCode, errorMessage);
  }

  public static object GetReplayLevelFilePickerStatus(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "operationId", out string operationId))
      return IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");

    return ReplayLevelFilePickerCoordinator.TryGetStatus(
      operationId,
      out ReplayLevelFilePickerStatus status,
      out string errorCode,
      out string errorMessage
    )
      ? ReplayLevelFilePickerStatusDto.From(status)
      : IpcDomainError.Create(errorCode, errorMessage);
  }

  private static int Offset(IpcRequest r) => Math.Max(0, IpcParams.OptionalInt(r, "offset") ?? 0);

  private static int Limit(IpcRequest r) => Math.Min(1000, Math.Max(1, IpcParams.OptionalInt(r, "limit") ?? 200));
}
