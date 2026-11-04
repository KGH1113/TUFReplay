namespace TUFReplay.Replay.Playback;

public readonly struct ReplayPlaybackSnapshot
{
  public ReplayPlaybackSnapshot(
    long timelineTimeUs,
    double timelineRate,
    double gameplayRate,
    long? wonTimeUs,
    bool paused,
    bool microphoneAudible = true,
    double dspTime = double.NaN,
    long gameInputOffsetUs = 0L
  )
  {
    TimelineTimeUs = timelineTimeUs;
    TimelineRate = timelineRate;
    GameplayRate = gameplayRate;
    WonTimeUs = wonTimeUs;
    Paused = paused;
    MicrophoneAudible = microphoneAudible;
    DspTime = dspTime;
    GameInputOffsetUs = gameInputOffsetUs;
  }

  public long TimelineTimeUs { get; }
  public double TimelineRate { get; }
  public double GameplayRate { get; }
  public long? WonTimeUs { get; }
  public bool Paused { get; }
  public bool MicrophoneAudible { get; }

  // Audio DSP time at which TimelineTimeUs is exact. The timeline advances at
  // TimelineRate per DSP second from here; NaN when it is not DSP based.
  public double DspTime { get; }

  // ADOFAI's input offset applied to this timeline. The game judges input
  // against song audio mixed this much earlier, so audio recorded alongside an
  // input must be mixed that much earlier to be heard where it was judged.
  public long GameInputOffsetUs { get; }

  public bool HasDspTime => !double.IsNaN(DspTime) && !double.IsInfinity(DspTime);

  public long TimelineTimeAtDspUs(double dspTime) =>
    HasDspTime ? TimelineTimeUs + (long)((dspTime - DspTime) * TimelineRate * 1_000_000d) : TimelineTimeUs;
}
