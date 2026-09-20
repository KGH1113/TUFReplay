using System;
using System.Diagnostics;

namespace TUFReplay.Microphone.Timing;

public readonly struct MicrophoneTimelineAnchor
{
  public readonly long CaptureTimestampTicks;
  public readonly long TimelineTimeUs;
  public readonly double GameplayRate;

  public MicrophoneTimelineAnchor(long captureTimestampTicks, long timelineTimeUs, double gameplayRate)
  {
    CaptureTimestampTicks = captureTimestampTicks;
    TimelineTimeUs = timelineTimeUs;
    GameplayRate = gameplayRate;
  }

  // Both timestamps belong to the native input Stopwatch clock. Convert the first
  // WAV sample to real seconds relative to gameplay zero, including the frozen wait.
  public long ToCaptureStartOffsetUs(long firstSampleTimestampTicks)
  {
    if (CaptureTimestampTicks <= 0 || firstSampleTimestampTicks <= 0)
      throw new ArgumentOutOfRangeException(nameof(firstSampleTimestampTicks));
    if (GameplayRate <= 0d || double.IsNaN(GameplayRate) || double.IsInfinity(GameplayRate))
      throw new InvalidOperationException("The microphone timeline rate is invalid.");

    double captureDeltaUs = (firstSampleTimestampTicks - CaptureTimestampTicks) * 1_000_000d / Stopwatch.Frequency;
    return checked((long)Math.Round(TimelineTimeUs / GameplayRate + captureDeltaUs));
  }
}
