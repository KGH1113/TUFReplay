using System;
using System.Diagnostics;

namespace TUFReplay.Shared.Timing;

public readonly struct CaptureTimelineAnchor
{
  public CaptureTimelineAnchor(long timestampTicks, long timelineTimeUs, double gameplayRate)
  {
    TimestampTicks = timestampTicks;
    TimelineTimeUs = timelineTimeUs;
    GameplayRate = gameplayRate;
  }

  public long TimestampTicks { get; }
  public long TimelineTimeUs { get; }
  public double GameplayRate { get; }

  public long ToCaptureStartOffsetUs(long firstSampleTimestampTicks)
  {
    if (TimestampTicks <= 0 || firstSampleTimestampTicks <= 0)
      throw new ArgumentOutOfRangeException(nameof(firstSampleTimestampTicks));
    if (GameplayRate <= 0d || double.IsNaN(GameplayRate) || double.IsInfinity(GameplayRate))
      throw new InvalidOperationException("The capture timeline rate is invalid.");
    double deltaUs = (firstSampleTimestampTicks - TimestampTicks) * 1_000_000d / Stopwatch.Frequency;
    return checked((long)Math.Round(TimelineTimeUs / GameplayRate + deltaUs));
  }
}
