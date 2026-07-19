using System;

namespace TUFReplay.Application.Replay;

public static class ReplayMicrophoneClock
{
  public static double ToMicrophoneTimeUs(
    long replayTimeUs,
    double gameplayRate,
    long captureStartOffsetUs,
    long? wonTimeUs = null
  )
  {
    double rate =
      gameplayRate > 0d && !double.IsNaN(gameplayRate) && !double.IsInfinity(gameplayRate) ? gameplayRate : 1d;
    double elapsedUs =
      wonTimeUs.HasValue && replayTimeUs >= wonTimeUs.Value
        ? wonTimeUs.Value / rate + (replayTimeUs - wonTimeUs.Value)
        : replayTimeUs / rate;
    return elapsedUs - captureStartOffsetUs;
  }

  public static long ToFrame(
    long replayTimeUs,
    double timelineRate,
    long captureStartOffsetUs,
    int sampleRate,
    long frameCount,
    long? wonTimeUs = null
  )
  {
    if (sampleRate <= 0 || frameCount < 0)
      throw new ArgumentOutOfRangeException(nameof(sampleRate));

    double microphoneTimeUs = ToMicrophoneTimeUs(replayTimeUs, timelineRate, captureStartOffsetUs, wonTimeUs);
    if (microphoneTimeUs <= 0d)
      return 0L;

    double frame = microphoneTimeUs * sampleRate / 1_000_000d;
    if (frame >= frameCount)
      return frameCount;
    return Math.Max(0L, (long)frame);
  }
}
