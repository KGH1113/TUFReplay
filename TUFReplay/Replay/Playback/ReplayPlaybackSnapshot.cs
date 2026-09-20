namespace TUFReplay.Replay.Playback;

public readonly struct ReplayPlaybackSnapshot
{
  public ReplayPlaybackSnapshot(
    long timelineTimeUs,
    double timelineRate,
    double gameplayRate,
    long? wonTimeUs,
    bool paused,
    bool microphoneAudible = true
  )
  {
    TimelineTimeUs = timelineTimeUs;
    TimelineRate = timelineRate;
    GameplayRate = gameplayRate;
    WonTimeUs = wonTimeUs;
    Paused = paused;
    MicrophoneAudible = microphoneAudible;
  }

  public long TimelineTimeUs { get; }
  public double TimelineRate { get; }
  public double GameplayRate { get; }
  public long? WonTimeUs { get; }
  public bool Paused { get; }
  public bool MicrophoneAudible { get; }
}
