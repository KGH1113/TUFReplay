using System;
using System.Diagnostics;
using TUFReplay.Composition;
using TUFReplay.Recording.Input;
using TUFReplay.Shared.Timing;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Recording;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
{
  private bool _webcamCaptureStarted;
  private WebcamCaptureTimeline _webcamTimeline;
  private PendingWebcamDisposition _pendingEditorWebcamRecording;
  private bool _persistFailedWebcam;

  private void StartWebcamRun()
  {
    long startTicks = RecordInputTracker.CaptureStartTimestampTicks;
    if (_calibrationRun || _currentRun == null || _webcamCaptureStarted || !Session.IsCapturingInput || startTicks <= 0)
      return;
    _webcamCaptureStarted = FeatureRegistry.WebcamRecording?.BeginRun(_currentRun.Id, startTicks) == true;
    if (_webcamCaptureStarted)
      _webcamTimeline = new WebcamCaptureTimeline();
  }

  private void ObserveWebcamTimeline()
  {
    if (!_webcamCaptureStarted)
      return;
    if (Session.TryGetInputTimelineAnchor(out long ticks, out long timeUs, out double rate))
      _webcamTimeline.Observe(new CaptureTimelineAnchor(ticks, timeUs, rate));
    else
      _webcamTimeline.NoteUnavailableAnchor();
  }

  private void ObserveWebcamWon()
  {
    if (_webcamCaptureStarted && Session.Data.WonTimeUs.HasValue)
      _webcamTimeline.Observe(new CaptureTimelineAnchor(Stopwatch.GetTimestamp(), Session.Data.WonTimeUs.Value, 1d));
  }

  private void ObserveWebcamFailure()
  {
    if (_webcamCaptureStarted && Session.Data.TerminalTimeUs.HasValue)
      _webcamTimeline.Observe(
        new CaptureTimelineAnchor(Stopwatch.GetTimestamp(), Session.Data.TerminalTimeUs.Value, 1d)
      );
  }

  private void EndWebcamRun(bool persist, Action<WebcamRecording> completed = null)
  {
    if (!_webcamCaptureStarted)
    {
      completed?.Invoke(null);
      return;
    }
    _webcamCaptureStarted = false;
    WebcamCaptureTimeline timeline = _webcamTimeline;
    _webcamTimeline = null;
    var camera = FeatureRegistry.WebcamRecording;
    var pending =
      completed == null
        ? new PendingWebcamDisposition(
          (recording, keep) => CompleteWebcamDisposition(recording, keep, camera),
          RunPersistence
        )
        : null;
    pending?.CompleteDisposition(persist);
    camera?.EndRun(
      timeline,
      recording =>
      {
        if (completed != null)
          completed(recording);
        else
          pending.CompleteCapture(recording);
      }
    );
  }

  private void QueueEditorWebcamRecording()
  {
    var camera = FeatureRegistry.WebcamRecording;
    var pending = new PendingWebcamDisposition(
      (recording, keep) => CompleteWebcamDisposition(recording, keep, camera),
      RunPersistence
    );
    _pendingEditorWebcamRecording = pending;
    EndWebcamRun(false, pending.CompleteCapture);
  }

  private static void CompleteWebcamDisposition(WebcamRecording recording, bool persist, WebcamRecordingFeature camera)
  {
    if (persist && camera != null)
      camera.Persist(recording);
    else
      WebcamRecordingStore.Discard(recording);
  }
}
