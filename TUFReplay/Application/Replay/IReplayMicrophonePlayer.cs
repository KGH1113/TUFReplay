using System;

namespace TUFReplay.Application.Replay;

public interface IReplayMicrophonePlayer : IDisposable
{
  void ResetTo(long replayTimeUs, double timelineRate);
  void Tick(long replayTimeUs, double timelineRate, bool paused);
  void Stop();
}
