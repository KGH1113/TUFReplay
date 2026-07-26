using AdofaiIpc.Core;
using TUFReplay.Application.Export;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class RunExportIpcHandlers
{
  public static object Export(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");

    RunExportResult result = RunExportCoordinator.Export(runId);
    if (result.Outcome == RunExportOutcomes.Error)
      return IpcDomainError.Create(result.ErrorCode, result.Message);
    return RunExportResultDto.From(result);
  }
}
