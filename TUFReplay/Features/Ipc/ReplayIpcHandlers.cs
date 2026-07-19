using AdofaiIpc.Core;
using TUFReplay.Application.Replay;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class ReplayIpcHandlers
{
  public static object Play(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    string levelPath = IpcParams.OptionalString(request, "levelPath");
    return ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.Play(runId, levelPath));
  }

  public static object GetStatus(IpcRequest request) =>
    ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus());

  public static object StartLevelFilePicker(IpcRequest request)
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

  public static object GetLevelFilePickerStatus(IpcRequest request)
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
}
