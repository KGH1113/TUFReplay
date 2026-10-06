using AdofaiIpc;
using AdofaiIpc.Core;
using TUFReplay.Replay.Ipc;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Replay.Ipc;

public static class ReplayIpcHandlers
{
  public static object Play(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    string levelPath = IpcParams.OptionalString(request, "levelPath");
    return ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.Play(runId, levelPath));
  }

  public static object GetStatus(IpcCommand request) =>
    ReplayPlaybackStatusDto.From(ReplayPlaybackCoordinator.GetStatus());

  public static object PickLevelFile(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    string purpose = IpcParams.OptionalString(request, "purpose") ?? "replay";
    if (purpose != "replay" && purpose != "render")
      return IpcDomainError.Create("invalid_picker_purpose", "purpose must be replay or render.");

    return ReplayLevelFilePickerResultDto.From(
      ReplayLevelFilePickerCoordinator.Start(runId, holdForReplay: purpose == "replay")
    );
  }

  public static object GetLevelFilePickerStatus(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "operationId", out string operationId))
      return IpcDomainError.Create("invalid_operation_id", "operationId must be a non-empty string.");

    return ReplayLevelFilePickerResultDto.From(ReplayLevelFilePickerCoordinator.GetStatus(operationId));
  }
}
