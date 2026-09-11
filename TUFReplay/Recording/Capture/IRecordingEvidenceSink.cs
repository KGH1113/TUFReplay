using TUFReplay.Replay.Models;

namespace TUFReplay.Recording.Capture;

/// <summary>Nonblocking observer. Implementations must not throw, allocate, or perform I/O in Write.</summary>
public interface IRecordingEvidenceSink
{
  void Write(RecordedInput input);
  void Write(RecordedHitContext hit);
  void Write(RecordingStateRecord state);
  void Invalidate(string reason);
}
