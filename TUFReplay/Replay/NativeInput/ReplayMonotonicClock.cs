using System.Diagnostics;

namespace TUFReplay.Replay.NativeInput;

internal interface IReplayMonotonicClock
{
  long Timestamp { get; }
  long Frequency { get; }
}

internal sealed class StopwatchReplayMonotonicClock : IReplayMonotonicClock
{
  public static readonly StopwatchReplayMonotonicClock Instance = new StopwatchReplayMonotonicClock();

  public long Timestamp => Stopwatch.GetTimestamp();
  public long Frequency => Stopwatch.Frequency;
}
