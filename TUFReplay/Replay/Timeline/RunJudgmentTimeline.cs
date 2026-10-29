using System;
using System.Collections.Generic;
using TUFReplay.Activity.Models;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Timeline;

/// <summary>A saved run's non-perfect judgments, placed on the timeline the replay HUD draws.</summary>
internal sealed class RunJudgmentTimelineData
{
  public string RunId;
  public int DurationMs;
  public int[] TimesMs;
  public string[] Kinds;
}

internal static class RunJudgmentTimelineBuilder
{
  /// <summary>
  /// Uses the same length as the in-game timeline (the clear time, otherwise the recorded end) and
  /// leaves out Perfect, Perfect-, X-Perfect and Perfect+ so the remaining marks show mistakes.
  /// </summary>
  internal static RunJudgmentTimelineData Build(
    string runId,
    string result,
    long? wonTimeUs,
    long? terminalTimeUs,
    RunJudgmentSystem judgmentSystem,
    IReadOnlyList<ReplayHitContext> hits
  )
  {
    long lastHitUs = 0L;
    for (int i = 0; i < hits.Count; i++)
      lastHitUs = Math.Max(lastHitUs, hits[i].TimeUs);

    long durationUs =
      string.Equals(result, "cleared", StringComparison.OrdinalIgnoreCase) && wonTimeUs is long won && won > 0L
        ? won
        : Math.Max(lastHitUs, terminalTimeUs ?? 0L);

    var times = new List<int>();
    var kinds = new List<string>();
    for (int i = 0; i < hits.Count; i++)
    {
      ReplayHitContext hit = hits[i];
      if (
        !ReplayTimelineJudgmentMath.TryMapHitMarginValue(
          hit.ResolvedHitMargin,
          judgmentSystem,
          out ReplayTimelineJudgmentKind kind
        ) || IsPerfect(kind)
      )
        continue;
      times.Add(ToMilliseconds(Math.Max(0L, Math.Min(hit.TimeUs, durationUs))));
      kinds.Add(kind.ToString());
    }

    return new RunJudgmentTimelineData
    {
      RunId = runId,
      DurationMs = ToMilliseconds(durationUs),
      TimesMs = times.ToArray(),
      Kinds = kinds.ToArray(),
    };
  }

  private static bool IsPerfect(ReplayTimelineJudgmentKind kind) =>
    kind
      is ReplayTimelineJudgmentKind.Perfect
        or ReplayTimelineJudgmentKind.PerfectMinus
        or ReplayTimelineJudgmentKind.XPerfect
        or ReplayTimelineJudgmentKind.PerfectPlus;

  private static int ToMilliseconds(long timeUs) => (int)Math.Min(int.MaxValue, Math.Max(0L, timeUs / 1000L));
}
