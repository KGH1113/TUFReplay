using System;
using System.IO;
using System.Text;
using System.Threading;
using AdofaiIpc;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Microphone.Repositories;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Microphone.Ipc;

public static class MicrophoneRecordingIpcHandlers
{
  public static IpcFile Export(string runId, System.Threading.CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");

    long? byteLength = MicrophoneRecordingRepository.GetByteLength(runId);
    if (!byteLength.HasValue)
      throw IpcDomainError.Create("microphone_recording_not_found", "Microphone recording was not found.");

    return IpcFile.FromWriter(
      destination =>
      {
        if (MicrophoneRecordingRepository.WriteTo(runId, destination, cancellationToken, byteLength.Value) == null)
          throw new FileNotFoundException("Microphone recording was removed before download.");
      },
      byteLength.Value,
      CreateDownloadFileName(runId),
      "audio/wav"
    );
  }

  public static MicrophoneRecordingDeleteResultDto Delete(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    if (RunRepository.IsLocked(runId))
      throw IpcDomainError.Create("run_locked", "Unlock this run to delete its microphone recording.");
    bool deleted = MicrophoneRecordingRepository.Delete(runId);
    ActivityChanges.Notify(runId);
    return new MicrophoneRecordingDeleteResultDto { RunId = runId, Deleted = deleted };
  }

  public static MicrophoneRecordingKeepResultDto KeepPermanently(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!MicrophoneRecordingRepository.RunExists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    if (!MicrophoneRecordingRepository.Exists(runId))
      throw IpcDomainError.Create("microphone_recording_not_found", "Microphone recording was not found.");

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
