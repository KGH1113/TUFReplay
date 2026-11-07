namespace TUFReplay.Microphone.Playback;

internal enum ReplayMicrophoneSourceCheckResult
{
  None,
  Restart,
  GiveUp,
}

// Watches where Unity actually plays the microphone source. On Windows, a
// fresh streamed clip can ignore the start position set before PlayScheduled
// and play from its beginning; nothing else notices, because the timeline
// check compares only the schedule. A source that stays off its schedule is
// restarted, the same way a focus change used to fix it.
internal sealed class ReplayMicrophoneSourceCheck
{
  // Consecutive off-schedule observations before a restart.
  public const int ConfirmObservations = 3;

  // Restarts in a row that did not bring the source back on schedule.
  public const int MaximumRestarts = 3;

  private int _offScheduleObservations;
  private int _restartsWithoutSuccess;

  public ReplayMicrophoneSourceCheck(int thresholdFrames)
  {
    ThresholdFrames = thresholdFrames;
  }

  public int ThresholdFrames { get; }

  public bool Disabled { get; private set; }

  public void OnStarted() => _offScheduleObservations = 0;

  public ReplayMicrophoneSourceCheckResult Observe(long sourceFrame, long scheduledFrame)
  {
    if (Disabled)
      return ReplayMicrophoneSourceCheckResult.None;

    if (!ReplayMicrophonePlaybackDecisions.IsSourceOffSchedule(sourceFrame, scheduledFrame, ThresholdFrames))
    {
      _offScheduleObservations = 0;
      _restartsWithoutSuccess = 0;
      return ReplayMicrophoneSourceCheckResult.None;
    }

    if (++_offScheduleObservations < ConfirmObservations)
      return ReplayMicrophoneSourceCheckResult.None;

    _offScheduleObservations = 0;
    if (_restartsWithoutSuccess >= MaximumRestarts)
    {
      // Unity does not report positions this check can trust here; stop
      // restarting instead of cutting the microphone out over and over.
      Disabled = true;
      return ReplayMicrophoneSourceCheckResult.GiveUp;
    }

    _restartsWithoutSuccess++;
    return ReplayMicrophoneSourceCheckResult.Restart;
  }
}
