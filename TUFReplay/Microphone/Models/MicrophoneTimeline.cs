namespace TUFReplay.Microphone.Models;

// Maps the gameplay timeline to time in the recorded WAV. Points within one
// section are interpolated; a section starts wherever gameplay resumed after a
// pause or changed rate, so audio captured while the timeline stood still is
// skipped. Timeline values before the first point extrapolate from it.
public sealed class MicrophoneTimeline
{
  public MicrophoneTimelinePoint[] Points;

  // WAV time before which the recording only holds audio captured while an
  // EnhancedCountdown frozen start was waiting for the first input.
  public long? AudibleFromUs;
}

public sealed class MicrophoneTimelinePoint
{
  public long TimelineTimeUs;
  public long AudioTimeUs;
  public double GameplayRate;
  public bool StartsSection;
}
