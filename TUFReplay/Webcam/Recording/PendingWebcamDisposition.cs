using System;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Recording;

public sealed class PendingWebcamDisposition
{
  private readonly object _gate = new object();
  private readonly Action<WebcamRecording, bool> _complete;
  private readonly Task<bool> _activityPersistence;
  private WebcamRecording _recording;
  private bool _captured;
  private bool? _persist;

  public PendingWebcamDisposition(Action<WebcamRecording, bool> complete, Task<bool> activityPersistence = null)
  {
    _complete = complete ?? throw new ArgumentNullException(nameof(complete));
    _activityPersistence = activityPersistence;
  }

  public void CompleteCapture(WebcamRecording recording)
  {
    bool? persist;
    lock (_gate)
    {
      if (_captured)
        return;
      _captured = true;
      _recording = recording;
      persist = _persist;
    }
    if (persist.HasValue)
      Dispatch(recording, persist.Value);
  }

  public void CompleteDisposition(bool persist)
  {
    WebcamRecording recording;
    lock (_gate)
    {
      if (_persist.HasValue)
        return;
      _persist = persist;
      if (!_captured)
        return;
      recording = _recording;
    }
    Dispatch(recording, persist);
  }

  private void Dispatch(WebcamRecording recording, bool persist)
  {
    if (_activityPersistence == null)
    {
      _complete(recording, persist);
      return;
    }
    // Wait outside the capture command queue: a locked activity database must
    // not delay opening the encoder for the next attempt.
    Task<bool> prerequisite = persist ? _activityPersistence : Task.FromResult(false);
    _ = prerequisite.ContinueWith(
      saved =>
      {
        _ = saved.Exception;
        _complete(recording, saved.Status == TaskStatus.RanToCompletion && saved.Result);
      },
      CancellationToken.None,
      TaskContinuationOptions.None,
      TaskScheduler.Default
    );
  }
}
