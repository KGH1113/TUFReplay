using AdofaiIpc;
using Newtonsoft.Json.Linq;
using TUFReplay.Replay.Export;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Replay.Ipc;

public static class RenderBundleIpcHandlers
{
  public static object Export(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "Select a play record to render.");
    if (
      !TryOptionalBool(request, "includeWebcam", out bool webcam)
      || !TryOptionalBool(request, "includeMicrophone", out bool microphone)
    )
      return IpcDomainError.Create("invalid_render_options", "Choose which recordings to include, then try again.");
    return RenderBundleExportService.Start(runId, IpcParams.OptionalString(request, "levelPath"), webcam, microphone);
  }

  public static object GetStatus(IpcCommand request) =>
    IpcParams.TryRequiredString(request, "jobId", out string jobId)
      ? RenderBundleExportService.GetStatus(jobId)
      : IpcDomainError.Create("invalid_job_id", "Start preparing a recording first.");

  public static object Cancel(IpcCommand request) =>
    IpcParams.TryRequiredString(request, "jobId", out string jobId)
      ? RenderBundleExportService.Cancel(jobId)
      : IpcDomainError.Create("invalid_job_id", "Start preparing a recording first.");

  private static bool TryOptionalBool(IpcCommand request, string name, out bool value)
  {
    JToken token = (request?.Payload as JObject)?[name];
    value = token == null || token.Type == JTokenType.Boolean && token.Value<bool>();
    return token == null || token.Type == JTokenType.Boolean;
  }
}
