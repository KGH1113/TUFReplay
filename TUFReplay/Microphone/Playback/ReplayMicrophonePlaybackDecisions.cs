using System;

namespace TUFReplay.Microphone.Playback;

internal static class ReplayMicrophonePlaybackDecisions
{
  // The microphone plays only while gameplay runs: not in the pre-roll, whose
  // time can jump when PlayerControl begins (especially after an
  // EnhancedCountdown frozen middle start), and not while paused.
  public static bool ShouldPlay(bool paused, bool microphoneAudible) => !paused && microphoneAudible;

  // A playing schedule moves once the replay timeline maps this far from it,
  // for example across a recorded pause or after a timeline jump the session
  // did not reset.
  public static bool ShouldReanchor(double timelineFrame, double scheduledFrame, int thresholdFrames) =>
    Math.Abs(timelineFrame - scheduledFrame) > thresholdFrames;
}
