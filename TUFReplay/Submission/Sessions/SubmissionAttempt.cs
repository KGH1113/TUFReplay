using System;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Capture;
using TUFReplay.Submission.Debug;
using TUFReplay.Submission.Transport;

namespace TUFReplay.Submission.Sessions;

/// <summary>Owns exactly one capture and upload lifetime.</summary>
public sealed class SubmissionAttempt : IDisposable
{
  private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
  private string _state = "uploading";
  public EvidenceCaptureBuffer Capture { get; } = new EvidenceCaptureBuffer();
  public Guid RunId { get; }
  public string State => Volatile.Read(ref _state);
  public bool Finished => State != "uploading";

  public SubmissionAttempt(IssuedRun run)
  {
    RunId = run.Id;
    _ = Task.Run(async () =>
    {
      try
      {
        var uploader = new EvidenceUploader(
          () => new WebSocketUploadConnection(run.WebSocketUrl, run.UploadToken),
          Capture,
          progress: progress => SubmissionDebugTelemetry.Publish(RunId, progress)
        );
        await uploader.Run(_cancellation.Token).ConfigureAwait(false);
        Volatile.Write(ref _state, "sealed");
      }
      catch (OperationCanceledException)
      {
        SubmissionDebugTelemetry.Publish("Upload cancelled");
        Volatile.Write(ref _state, "cancelled");
      }
      catch (UploadRejectedException error)
      {
        SubmissionDebugTelemetry.Publish("Upload rejected: " + error.Message);
        Capture.Invalidate(error.Message);
        Volatile.Write(ref _state, "unavailable");
      }
      catch (Exception)
      {
        SubmissionDebugTelemetry.Publish("Upload failed");
        Capture.Invalidate("upload_failed");
        Volatile.Write(ref _state, "unavailable");
      }
    });
  }

  public void Dispose()
  {
    Capture.Invalidate("attempt_cancelled");
    _cancellation.Cancel();
  }
}
