using TUFReplay.Shared.Timing;

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
    return new CaptureTimelineAnchor(CaptureTimestampTicks, TimelineTimeUs, GameplayRate).ToCaptureStartOffsetUs(
      firstSampleTimestampTicks
    );
  }
}
