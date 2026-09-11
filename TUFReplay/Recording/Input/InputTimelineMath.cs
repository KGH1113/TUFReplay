namespace TUFReplay.Recording.Input;

internal static class InputTimelineMath
{
  internal static long Interpolate(
    long eventTicks,
    long previousTicks,
    long previousTimeUs,
    long currentTicks,
    long currentTimeUs
  )
  {
    if (currentTicks <= previousTicks)
      return currentTimeUs;

    double fraction = (double)(eventTicks - previousTicks) / (currentTicks - previousTicks);
    return previousTimeUs + (long)((currentTimeUs - previousTimeUs) * fraction);
  }

  internal static long BackProject(long eventTicks, long anchorTicks, long anchorTimeUs, double timelineRate)
  {
    double elapsedUs = (anchorTicks - eventTicks) * 1_000_000d / StopwatchFrequency;
    return anchorTimeUs - (long)(elapsedUs * timelineRate);
  }

  private static readonly double StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency;
}
