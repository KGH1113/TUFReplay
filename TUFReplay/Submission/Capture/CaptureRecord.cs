using TUFReplay.Replay.Models;
using TUFReplay.Recording.Capture;

namespace TUFReplay.Submission.Capture;

public readonly struct CaptureRecord
{
  public readonly byte Kind;
  public readonly RecordedInput Input;
  public readonly RecordedHitContext Hit;
  public readonly RecordingStateRecord State;

  public CaptureRecord(RecordedInput input) { Kind = 0; Input = input; Hit = default; State = default; }
  public CaptureRecord(RecordedHitContext hit) { Kind = 1; Hit = hit; Input = default; State = default; }
  public CaptureRecord(RecordingStateRecord state)
  { Kind = state.State == RecordingStateKind.RuntimeSettings ? (byte)4 : state.State == RecordingStateKind.RecorderHealth ? (byte)5 : (byte)3;
    State = state; Input = default; Hit = default; }
}
