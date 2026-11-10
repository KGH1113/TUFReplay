using System;
using System.Collections.Generic;

namespace TUFReplay.Replay.Rendering;

// Replay time follows the original pitch until the clear or the death, then advances at wall-clock speed.
internal sealed class RenderTimeline
{
  public double GameplayStartUs { get; }
  public double GameplayRate { get; }
  public long? GameplayEndTimeUs { get; }

  public RenderTimeline(double gameplayStartUs, double gameplayRate, long? gameplayEndTimeUs)
  {
    if (
      double.IsNaN(gameplayStartUs)
      || double.IsInfinity(gameplayStartUs)
      || gameplayRate <= 0
      || double.IsNaN(gameplayRate)
      || double.IsInfinity(gameplayRate)
    )
      throw new ArgumentOutOfRangeException(nameof(gameplayRate));
    GameplayStartUs = gameplayStartUs;
    GameplayRate = gameplayRate;
    GameplayEndTimeUs = gameplayEndTimeUs;
  }

  public double ReplayToOutput(double replayUs) =>
    GameplayStartUs
    + (
      GameplayEndTimeUs.HasValue && replayUs >= GameplayEndTimeUs.Value
        ? GameplayEndTimeUs.Value / GameplayRate + replayUs - GameplayEndTimeUs.Value
        : replayUs / GameplayRate
    );

  public double OutputToReplay(double outputUs)
  {
    double elapsed = outputUs - GameplayStartUs;
    double endOutput = GameplayEndTimeUs.HasValue ? GameplayEndTimeUs.Value / GameplayRate : double.PositiveInfinity;
    return elapsed >= endOutput ? GameplayEndTimeUs.Value + elapsed - endOutput : elapsed * GameplayRate;
  }

  public double RateAtOutput(double outputUs) =>
    GameplayEndTimeUs.HasValue && outputUs >= ReplayToOutput(GameplayEndTimeUs.Value) ? 1 : GameplayRate;

  public object[] ToSegments()
  {
    var segments = new List<object>
    {
      new
      {
        outputTimeUs = 0L,
        replayTimeUs = (long)Math.Round(-GameplayStartUs * GameplayRate),
        rate = GameplayRate,
      },
    };
    if (GameplayEndTimeUs.HasValue)
      segments.Add(
        new
        {
          outputTimeUs = (long)Math.Round(ReplayToOutput(GameplayEndTimeUs.Value)),
          replayTimeUs = GameplayEndTimeUs.Value,
          rate = 1d,
        }
      );
    return segments.ToArray();
  }
}
