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
}
