using System;
using System.IO;
using System.Text;
using System.Threading;
using AdofaiIpc;
using AdofaiIpc.Core;
using TUFReplay.Activity.Tracking;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Microphone.Ipc;

public static class MicrophoneRecordingIpcHandlers
{
  public static object Export(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return new IpcDownloadError("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return new IpcDownloadError("run_not_found", "Run was not found.", 404);

    long? byteLength = MicrophoneRecordingRepository.GetByteLength(runId);
    if (!byteLength.HasValue)
      return new IpcDownloadError("microphone_recording_not_found", "Microphone recording was not found.", 404);

    return new IpcDownloadSource(
      destination =>
      {
        if (MicrophoneRecordingRepository.WriteTo(runId, destination, CancellationToken.None, byteLength.Value) == null)
          throw new FileNotFoundException("Microphone recording was removed before download.");
      },
      byteLength.Value,
      CreateDownloadFileName(runId),
      "audio/wav"
    );
  }

  public static object Delete(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    bool deleted = MicrophoneRecordingRepository.Delete(runId);
    ActivityChanges.Notify(runId);
    return new MicrophoneRecordingDeleteResultDto { RunId = runId, Deleted = deleted };
  }

  public static object KeepPermanently(IpcCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    if (!MicrophoneRecordingRepository.Exists(runId))
      return IpcDomainError.Create("microphone_recording_not_found", "Microphone recording was not found.");

    MicrophoneRecordingRepository.KeepPermanently(runId);
    ActivityChanges.Notify(runId);
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
