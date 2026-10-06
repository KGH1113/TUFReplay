using System;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Webcam.Playback;

public interface IReplayWebcamPlayer : IDisposable
{
  void Tick(ReplayPlaybackSnapshot snapshot);
  void ResetTo(ReplayPlaybackSnapshot snapshot);
  void Stop();
}
