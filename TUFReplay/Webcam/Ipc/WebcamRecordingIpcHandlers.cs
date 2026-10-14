using System.IO;
using System.Text;
using TUFReplay.Activity.Repositories;
using TUFReplay.Activity.Tracking;
using TUFReplay.Composition;
using TUFReplay.Replay.Preparation;
using TUFReplay.Shared.Ipc;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Ipc;

public static class WebcamRecordingIpcHandlers
{
  public static object Export(JsonCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return new DownloadFailure("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      return new DownloadFailure("run_not_found", "Run was not found.", 404);
    WebcamRecordingStore store = FeatureRegistry.WebcamRecording?.Store;
    var recording = store?.GetMetadata(runId);
    if (recording == null)
      return new DownloadFailure("camera_recording_not_found", "Camera recording was not found.", 404);
    long bytes = new FileInfo(recording.FilePath).Length;
    return new DownloadSource(
      destination => store.WriteTo(runId, destination, bytes),
      bytes,
      CreateDownloadFileName(runId),
      "video/mp4"
    );
  }

  public static object Delete(JsonCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    if (ReplayPlaybackCoordinator.IsBusy && ReplayPlaybackCoordinator.GetStatus().RunId == runId)
      return IpcDomainError.Create("run_in_use", "Stop this replay before deleting its camera video.");
    bool deleted = FeatureRegistry.WebcamRecording?.Store?.DeleteRun(runId) == true;
    ActivityChanges.Notify(runId);
    return new WebcamRecordingDeleteResultDto { RunId = runId, Deleted = deleted };
  }

  public static object KeepPermanently(JsonCommand request)
  {
    if (!IpcParams.TryRequiredString(request, "runId", out string runId))
      return IpcDomainError.Create("invalid_run_id", "runId must be a non-empty string.");
    if (!RunRepository.Exists(runId))
      return IpcDomainError.Create("run_not_found", "Run was not found.");
    if (FeatureRegistry.WebcamRecording?.Store?.KeepPermanently(runId) != true)
      return IpcDomainError.Create("camera_recording_not_found", "Camera recording was not found.");
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
