namespace TUFReplay.Recording.Capture;

public enum RecordingStateKind { CaptureStarted, GameplayStarted, Discontinuity, Won, RuntimeSettings, RecorderHealth }

/// <summary>Value-only state observation suitable for the nonblocking evidence sink.</summary>
public readonly struct RecordingStateRecord
{
  public readonly RecordingStateKind State;
  public readonly long TimeUs;
  public readonly double Rate;
  public readonly bool NoFail;
  public readonly int Difficulty;
  public readonly long Dropped;
  public readonly long Unmapped;
  public readonly int HoldBehavior;
  public RecordingStateRecord(RecordingStateKind state, long timeUs, double rate = 1,
    bool noFail = false, int difficulty = -1, long dropped = 0, long unmapped = 0, int holdBehavior = -1)
  { State = state; TimeUs = timeUs; Rate = rate; NoFail = noFail; Difficulty = difficulty; Dropped = dropped; Unmapped = unmapped; HoldBehavior = holdBehavior; }
}
