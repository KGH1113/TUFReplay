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
    bool adjustTimeline = _microphoneTimelineAnchored;
    long timelineCorrectionUs = _microphoneTimelineCorrectionUs;
    _microphoneCaptureStarted = false;
    _microphoneCaptureStartedAt = null;
    _microphoneTimelineCorrectionUs = 0L;
    _microphoneTimelineAnchored = false;

    if (FeatureRegistry.MicrophoneRecording == null)
    {
      completed?.Invoke(null);
      return;
    }
    FeatureRegistry.MicrophoneRecording.EndRun(recording =>
    {
      if (recording != null && adjustTimeline)
        recording.CaptureStartOffsetUs += timelineCorrectionUs;
      completed?.Invoke(recording);
    });
  }

  private void StartMicrophoneRun()
  {
    if (_currentRun == null || _microphoneCaptureStarted)
      return;

    FeatureRegistry.MicrophoneRecording?.BeginRun(_currentRun.Id);
    _microphoneCaptureStarted = true;
    _microphoneCaptureStartedAt = RecordingClock.CurrentUnscaledTime();
  }

  internal void TryAnchorMicrophoneTimeline()
  {
    if (!_microphoneCaptureStarted || _microphoneTimelineAnchored || !Session.Data.GameplayStartSongPosition.HasValue)
      return;

    scrConductor conductor = ADOBase.conductor;
    if (conductor == null || !conductor.enabled || UnityEngine.Time.timeScale <= 0f || UnityEngine.AudioListener.pause)
      return;

    double now = RecordingClock.CurrentUnscaledTime();
    double startedAt = _microphoneCaptureStartedAt ?? now;
    double captureElapsedSeconds = Math.Max(0d, now - startedAt);
    long timelineTimeUs = Math.Max(
      0L,
      RecordingClock.ToRecordTimeUs(RecordingClock.CurrentSongPosition(), Session.Data.GameplayStartSongPosition)
    );
    double gameplayRate = Session.Data.EffectivePitch ?? conductor.song?.pitch ?? 1f;

    _microphoneTimelineCorrectionUs = MicrophoneTimelineAnchor.CalculateCorrectionUs(
      timelineTimeUs,
      gameplayRate,
      captureElapsedSeconds
    );
    _microphoneTimelineAnchored = true;
    Main.Instance.Log(
      "[Recording/Microphone] Timeline anchored. timelineUs="
        + timelineTimeUs
        + ", captureElapsedUs="
        + (long)(captureElapsedSeconds * 1_000_000d)
        + ", rate="
        + gameplayRate
        + ", correctionUs="
        + _microphoneTimelineCorrectionUs
    );
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
