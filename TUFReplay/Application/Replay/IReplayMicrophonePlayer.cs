using System;

namespace TUFReplay.Application.Replay;

public interface IReplayMicrophonePlayer : IDisposable
{
  void ResetTo(long replayTimeUs, double gameplayRate, long? wonTimeUs);
  void Tick(long replayTimeUs, double gameplayRate, long? wonTimeUs, bool paused);
  void UpdateLatency(int latencyMs, long replayTimeUs, double gameplayRate, long? wonTimeUs);
  void UpdateVolume(int volumeDb);
  void Stop();
}
