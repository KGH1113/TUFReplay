namespace TUFReplay.Application.Replay;

internal readonly struct ReplayTimelinePlaybackSnapshot
{
  public ReplayTimelinePlaybackSnapshot(
    string runId,
    long elapsedTimeUs,
    long durationTimeUs,
    bool paused,
    bool canTogglePause,
    bool canSeek
  )
  {
    RunId = runId;
    ElapsedTimeUs = elapsedTimeUs;
    DurationTimeUs = durationTimeUs;
    Paused = paused;
    CanTogglePause = canTogglePause;
    CanSeek = canSeek;
  }

  public string RunId { get; }
  public long ElapsedTimeUs { get; }
  public long DurationTimeUs { get; }
  public bool Paused { get; }
  public bool CanTogglePause { get; }
  public bool CanSeek { get; }
}
