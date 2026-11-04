using System;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Recording;
using TUFReplay.Microphone.Timing;
using TUFReplay.Recording.Microphone;
using TUFReplay.Webcam.Recording;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
{
  private void EndMicrophoneRun(
    Action<CapturedMicrophoneRecording> completed,
    bool persistWebcam = false,
    bool endWebcam = true
  )
  {
    if (endWebcam)
      EndWebcamRun(persistWebcam);
    if (!_microphoneCaptureStarted)
    {
      completed?.Invoke(null);
      return;
    }
    MicrophoneCaptureTimeline timeline = _microphoneTimeline;
    bool frozenStart = Session.GameplayStartedFrozen;
    _microphoneCaptureStarted = false;
    _microphoneTimeline = null;

    if (FeatureRegistry.MicrophoneRecording == null)
    {
      completed?.Invoke(null);
      return;
    }
    // The detached timeline receives no more observations, so the finalization
    // thread can read it.
    FeatureRegistry.MicrophoneRecording.EndRun(recording =>
    {
      if (recording != null && !TryAlignMicrophoneRecording(recording, timeline, frozenStart))
      {
        FeatureRegistry.MicrophoneRecording?.Discard(recording);
        completed?.Invoke(null);
        return;
      }
      completed?.Invoke(recording);
    });
  }

  private static bool TryAlignMicrophoneRecording(
    CapturedMicrophoneRecording recording,
    MicrophoneCaptureTimeline timeline,
    bool frozenStart
  )
  {
    if (timeline == null || !timeline.HasAnchor || recording.CaptureStartTimestampTicks <= 0)
    {
      Main.Instance?.Log("[Recording/Microphone] Discarded recording without a capture timeline anchor.");
      return false;
    }

    try
    {
      recording.Timeline = timeline.Build(recording.CaptureStartTimestampTicks, frozenStart);
      recording.CaptureStartOffsetUs = MicrophoneCaptureTimeline.ToCaptureStartOffsetUs(recording.Timeline);
    }
    catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
    {
      Main.Instance?.Log(
        "[Recording/Microphone] Discarded recording with an invalid timeline. error=" + exception.Message
      );
      return false;
    }

    MicrophoneTimelinePoint first = recording.Timeline.Points[0];
    Main.Instance?.Log(
      "[Recording/Microphone] Capture aligned. runId="
        + recording.RunId
        + ", timelineUs="
        + first.TimelineTimeUs
        + ", rate="
        + first.GameplayRate
        + ", captureOffsetUs="
        + recording.CaptureStartOffsetUs
        + ", points="
        + recording.Timeline.Points.Length
        + ", audibleFromUs="
        + (recording.Timeline.AudibleFromUs?.ToString() ?? "start")
    );
    return true;
  }

  private void StartMicrophoneRun()
  {
    if (_currentRun == null || _microphoneCaptureStarted)
      return;

    _microphoneCaptureStarted = FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id) == true;
    _microphoneTimeline = _microphoneCaptureStarted ? new MicrophoneCaptureTimeline() : null;
  }

  // Called only after a fresh, advancing conductor update. Reuse the exact pair
  // used to map native input; reading songposition in PlayerControl_Update can
  // pair a stale position with a later frame, especially on frozen-start release.
  internal void ObserveCaptureTimelines()
  {
    if (!_webcamCaptureStarted && _microphoneTimeline == null)
      return;
    if (!Session.TryGetInputTimelineAnchor(out long captureTicks, out long timelineUs, out double rate))
      return;
    ObserveWebcamTimeline(captureTicks, timelineUs, rate);
    _microphoneTimeline?.Observe(captureTicks, timelineUs, rate);
  }

  private void ObserveMicrophoneWon()
  {
    if (_microphoneTimeline != null && Session.Data.WonTimeUs.HasValue)
      _microphoneTimeline.ObserveWon(Session.Data.WonTimeUs.Value);
  }

  private void QueueEditorRecording()
  {
    DiscardPendingEditorRecording();
    QueueEditorWebcamRecording();
    var pending = new PendingMicrophoneDisposition(RecordingMicrophoneDisposition.Complete);
    _pendingEditorRecording = pending;
    EndMicrophoneRun(pending.CompleteCapture);
  }

  private void DiscardPendingEditorRecording()
  {
    PendingWebcamDisposition webcam = _pendingEditorWebcamRecording;
    _pendingEditorWebcamRecording = null;
    webcam?.CompleteDisposition(persist: false);
    PendingMicrophoneDisposition recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    recording?.CompleteDisposition(persist: false);
  }
}
