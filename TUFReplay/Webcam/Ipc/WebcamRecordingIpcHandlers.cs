using System.IO;
using System.Text;
using AdofaiIpc;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Ipc;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Ipc;

public static class WebcamRecordingIpcHandlers
{
  public static IpcFile Export(string runId, System.Threading.CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    WebcamRecordingStore store = FeatureRegistry.WebcamRecording?.Store;
    var recording = store?.GetMetadata(runId);
    if (recording == null)
      throw IpcDomainError.Create("camera_recording_not_found", "Camera recording was not found.");
    long bytes = new FileInfo(recording.FilePath).Length;
    return IpcFile.FromWriter(
      destination => store.WriteTo(runId, destination, bytes, cancellationToken),
      bytes,
      CreateDownloadFileName(runId),
      "video/mp4"
    );
  }

  public static WebcamRecordingDeleteResultDto Delete(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    if (RunRepository.IsLocked(runId))
      throw IpcDomainError.Create("run_locked", "Unlock this run to delete its camera video.");
    if (ReplayPlaybackCoordinator.IsBusy && ReplayPlaybackCoordinator.GetStatus().RunId == runId)
      throw IpcDomainError.Create("run_in_use", "Stop this replay before deleting its camera video.");
    bool deleted = FeatureRegistry.WebcamRecording?.Store?.DeleteRun(runId) == true;
    ActivityChanges.Notify(runId);
    return new WebcamRecordingDeleteResultDto { RunId = runId, Deleted = deleted };
  }

  public static WebcamRecordingKeepResultDto KeepPermanently(string runId)
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      throw IpcDomainError.Create("run_not_found", "Run was not found.");
    if (FeatureRegistry.WebcamRecording?.Store?.KeepPermanently(runId) != true)
      throw IpcDomainError.Create("camera_recording_not_found", "Camera recording was not found.");
    ActivityChanges.Notify(runId);
    return new WebcamRecordingKeepResultDto { RunId = runId, Permanent = true };
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
    return safeId.Length == 0 ? "tufreplay-camera.mp4" : "tufreplay-run-" + safeId + "-camera.mp4";
  }
}

public sealed class WebcamRecordingDeleteResultDto
{
  public string RunId;
  public bool Deleted;
}

public sealed class WebcamRecordingKeepResultDto
{
  public string RunId;
  public bool Permanent;
}
