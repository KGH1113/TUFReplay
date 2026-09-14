namespace TUFReplay.Replay.Playback;

public readonly struct ReplayPlaybackSnapshot
{
  public ReplayPlaybackSnapshot(
    long timelineTimeUs,
    double timelineRate,
    double gameplayRate,
    long? wonTimeUs,
    bool paused,
    long gameInputOffsetUs = 0L
  )
  {
    TimelineTimeUs = timelineTimeUs;
    TimelineRate = timelineRate;
    GameplayRate = gameplayRate;
    WonTimeUs = wonTimeUs;
    Paused = paused;
    GameInputOffsetUs = gameInputOffsetUs;
  }

  public long TimelineTimeUs { get; }
  public double TimelineRate { get; }
  public double GameplayRate { get; }
  public long? WonTimeUs { get; }
  public bool Paused { get; }
  public long GameInputOffsetUs { get; }
}
