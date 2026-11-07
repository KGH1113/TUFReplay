using System;

namespace TUFReplay.Microphone.Playback;

internal static class ReplayMicrophonePlaybackDecisions
{
  public static bool ShouldSuppressReset(
    long targetFrame,
    long lastResetFrame,
    int seekGeneration,
    int lastResetGeneration,
    bool preparing,
    long requestedFrame,
    bool sourceActive,
    long sourceFrame,
    int driftThresholdFrames
  )
  {
    if (targetFrame != lastResetFrame || seekGeneration != lastResetGeneration)
      return false;
    return (preparing && requestedFrame == targetFrame)
      || (sourceActive && Math.Abs(sourceFrame - targetFrame) < driftThresholdFrames);
  }

  public static bool IsCurrentRecovery(int recoveryGeneration, int activeGeneration) =>
    recoveryGeneration != 0 && recoveryGeneration == activeGeneration;

  public static bool ShouldAcceptReaderPosition(int activeGeneration) => activeGeneration == 0;

  public static bool ShouldMute(bool microphoneAudible) => !microphoneAudible;

  public static bool ShouldStartSource(bool microphoneAudible) => microphoneAudible;

  // A running source plays one frame per DSP sample period from its scheduled
  // start. Reschedule when the replay timeline maps elsewhere, such as across a
  // recorded pause or after a timeline jump the session did not reset.
  public static bool ShouldResync(
    long timelineFrame,
    long scheduledFrame,
    long audibleFromFrame,
    int thresholdFrames
  ) => Math.Abs(Math.Max(timelineFrame, audibleFromFrame) - scheduledFrame) > thresholdFrames;

  // Unity's reported source position trails the schedule by up to the frames
  // queued in the output buffers. Anything well past that means the source is
  // not playing where it was scheduled.
  public static int SourceCheckThresholdFrames(
    int sampleRate,
    int outputSampleRate,
    int dspBufferFrames,
    int dspBufferCount,
    int minimumMilliseconds
  )
  {
    int minimumFrames = Math.Max(1, sampleRate * minimumMilliseconds / 1000);
    double queuedFrames =
      Math.Max(1, dspBufferFrames)
      * (double)(Math.Max(1, dspBufferCount) + 2)
      * sampleRate
      / Math.Max(1, outputSampleRate);
    return Math.Max(minimumFrames, (int)Math.Ceiling(queuedFrames));
  }

  public static bool IsSourceOffSchedule(long sourceFrame, long scheduledFrame, int thresholdFrames) =>
    Math.Abs(sourceFrame - scheduledFrame) > thresholdFrames;
}
