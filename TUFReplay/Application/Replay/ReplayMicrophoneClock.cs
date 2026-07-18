using System;

namespace TUFReplay.Application.Replay;

public static class ReplayMicrophoneClock
{
  public static double ToMicrophoneTimeUs(long replayTimeUs, double timelineRate, long captureStartOffsetUs)
  {
    double rate =
      timelineRate > 0d && !double.IsNaN(timelineRate) && !double.IsInfinity(timelineRate) ? timelineRate : 1d;
    return replayTimeUs / rate - captureStartOffsetUs;
  }

  public static long ToFrame(
    long replayTimeUs,
    double timelineRate,
    long captureStartOffsetUs,
    int sampleRate,
    long frameCount
  )
  {
    if (sampleRate <= 0 || frameCount < 0)
      throw new ArgumentOutOfRangeException(nameof(sampleRate));

    double microphoneTimeUs = ToMicrophoneTimeUs(replayTimeUs, timelineRate, captureStartOffsetUs);
    if (microphoneTimeUs <= 0d)
      return 0L;

    double frame = microphoneTimeUs * sampleRate / 1_000_000d;
    if (frame >= frameCount)
      return frameCount;
    return Math.Max(0L, (long)frame);
  }
}
