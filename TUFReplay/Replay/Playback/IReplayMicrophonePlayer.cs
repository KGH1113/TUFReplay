using System;

namespace TUFReplay.Replay.Playback;

public interface IReplayMicrophonePlayer : IDisposable
{
  void ResetTo(ReplayPlaybackSnapshot snapshot);
  void Tick(ReplayPlaybackSnapshot snapshot);
  void UpdateLatency(int latencyMs, ReplayPlaybackSnapshot snapshot);
  void UpdateVolume(int volumeDb);
  void Stop();
}
