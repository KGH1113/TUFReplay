using System;

namespace TUFReplay.Application.Microphone;

public static class MicrophoneTimelineAnchor
{
  public static long CalculateCorrectionUs(long timelineTimeUs, double gameplayRate, double captureElapsedSeconds)
  {
    double rate =
      gameplayRate > 0d && !double.IsNaN(gameplayRate) && !double.IsInfinity(gameplayRate) ? gameplayRate : 1d;
    double elapsedSeconds =
      captureElapsedSeconds >= 0d && !double.IsNaN(captureElapsedSeconds) && !double.IsInfinity(captureElapsedSeconds)
        ? captureElapsedSeconds
        : 0d;
    double timelineElapsedUs = timelineTimeUs / rate;
    double captureElapsedUs = elapsedSeconds * 1_000_000d;
    return (long)(timelineElapsedUs - captureElapsedUs);
  }
}
