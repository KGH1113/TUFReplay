using System;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Recording;
using TUFReplay.Microphone.Timing;
using TUFReplay.Recording.Microphone;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingFeature
{
  private void EndMicrophoneRun(Action<CapturedMicrophoneRecording> completed)
  {
    if (!_microphoneCaptureStarted)
    {
      completed?.Invoke(null);
      return;
    }
    MicrophoneTimelineAnchor? anchor = _microphoneTimelineAnchor;
    _microphoneCaptureStarted = false;
    _microphoneTimelineAnchor = null;

    if (FeatureRegistry.MicrophoneRecording == null)
    {
      completed?.Invoke(null);
      return;
    }
    FeatureRegistry.MicrophoneRecording.EndRun(recording =>
    {
      if (recording != null)
      {
        if (!anchor.HasValue || recording.CaptureStartTimestampTicks <= 0)
        {
          Main.Instance?.Log("[Recording/Microphone] Discarded recording without a capture timeline anchor.");
          FeatureRegistry.MicrophoneRecording?.Discard(recording);
          completed?.Invoke(null);
          return;
        }

        recording.CaptureStartOffsetUs = anchor.Value.ToCaptureStartOffsetUs(recording.CaptureStartTimestampTicks);
        Main.Instance?.Log(
          "[Recording/Microphone] Capture aligned. runId="
            + recording.RunId
            + ", timelineUs="
            + anchor.Value.TimelineTimeUs
            + ", rate="
            + anchor.Value.GameplayRate
            + ", captureOffsetUs="
            + recording.CaptureStartOffsetUs
        );
      }
      completed?.Invoke(recording);
    });
  }

  private void StartMicrophoneRun()
  {
    if (_currentRun == null || _microphoneCaptureStarted)
      return;

    _microphoneCaptureStarted = FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id) == true;
  }

  internal void TryAnchorMicrophoneTimeline()
  {
    if (!_microphoneCaptureStarted || _microphoneTimelineAnchor.HasValue)
      return;

    // Called only after a fresh, advancing conductor update. Reuse the exact pair
    // used to map native input; reading songposition in PlayerControl_Update can
    // pair a stale position with a later frame, especially on frozen-start release.
    if (!Session.TryGetInputTimelineAnchor(out long captureTicks, out long timelineUs, out double rate))
      return;
    _microphoneTimelineAnchor = new MicrophoneTimelineAnchor(captureTicks, timelineUs, rate);
  }

  private void QueueEditorRecording()
  {
    DiscardPendingEditorRecording();
    var pending = new PendingMicrophoneDisposition(RecordingMicrophoneDisposition.Complete);
    _pendingEditorRecording = pending;
    EndMicrophoneRun(pending.CompleteCapture);
  }

  private void DiscardPendingEditorRecording()
  {
    PendingMicrophoneDisposition recording = _pendingEditorRecording;
    _pendingEditorRecording = null;
    recording?.CompleteDisposition(persist: false);
  }
}
