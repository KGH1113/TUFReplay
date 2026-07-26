using AdofaiIpc.Core;
using TUFReplay.Application.Export;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class MicrophoneRecordingIpcHandlers
{
  public static object Delete(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    if (RunExportCoordinator.IsExportingRun(runId))
      return IpcDomainError.Create("run_in_use", "Wait for this run export to finish before deleting its recording.");

    return new MicrophoneRecordingDeleteResultDto
    {
      RunId = runId,
      Deleted = MicrophoneRecordingRepository.Delete(runId),
    };
  }

  public static object KeepPermanently(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    if (!MicrophoneRecordingRepository.Exists(runId))
      return IpcDomainError.Create("microphone_recording_not_found", "Microphone recording was not found.");

    MicrophoneRecordingRepository.KeepPermanently(runId);
    return new MicrophoneRecordingKeepResultDto { RunId = runId, Permanent = true };
  }
}
