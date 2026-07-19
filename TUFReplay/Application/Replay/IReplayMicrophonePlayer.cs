using System;

namespace TUFReplay.Application.Replay;

public interface IReplayMicrophonePlayer : IDisposable
{
  void ResetTo(long replayTimeUs, double gameplayRate, long? wonTimeUs);
  void Tick(long replayTimeUs, double gameplayRate, long? wonTimeUs, bool paused);
  void UpdateUserSettings(int offsetMs, int volumeDb, long replayTimeUs, double gameplayRate, long? wonTimeUs);
  void Stop();
}
