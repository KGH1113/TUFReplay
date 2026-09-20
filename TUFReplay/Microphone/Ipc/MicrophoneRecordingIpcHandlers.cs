using System;
using System.IO;
using System.Text;
using System.Threading;
using AdofaiIpc;
using AdofaiIpc.Core;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Microphone.Ipc;

public static class MicrophoneRecordingIpcHandlers
{
  public static object Export(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return new IpcDownloadError("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return new IpcDownloadError("run_not_found", "Run was not found.", 404);

    long? byteLength = MicrophoneRecordingRepository.GetByteLength(runId);
    if (!byteLength.HasValue)
      return new IpcDownloadError("microphone_recording_not_found", "Microphone recording was not found.", 404);

    return new IpcDownloadResponse(
      "audio/wav",
      byteLength.Value,
      CreateDownloadFileName(runId),
      destination =>
      {
        if (MicrophoneRecordingRepository.WriteTo(runId, destination, CancellationToken.None, byteLength.Value) == null)
          throw new FileNotFoundException("Microphone recording was removed before download.");
      }
    );
  }

  public static object Delete(IpcRequest request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
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

  private static string CreateDownloadFileName(string runId)
  {
    var safeId = new StringBuilder(64);
    foreach (char character in runId)
    {
      if (safeId.Length == 64)
        break;
      if (
        (character >= 'a' && character <= 'z')
        || (character >= 'A' && character <= 'Z')
        || (character >= '0' && character <= '9')
        || character == '-'
        || character == '_'
      )
        safeId.Append(character);
    }

    return safeId.Length == 0 ? "tufreplay-microphone.wav" : "tufreplay-run-" + safeId + "-microphone.wav";
  }
}
