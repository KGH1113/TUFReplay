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

  public static object PickLevelFile(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    return ReplayLevelFilePickerResultDto.From(ReplayLevelFilePickerCoordinator.Pick(runId));
  }
}
