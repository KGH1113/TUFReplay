using TUFReplay.Activity.Models;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Timeline;
using static TestFixture;

internal static class RunJudgmentTimelineSuite
{
  internal static void RunAll()
  {
    LeavesOutPerfectsInCompetitiveRuns();
    LeavesOutPerfectsInClassicRuns();
    MatchesTheReplayTimelineLength();
  }

  private static void LeavesOutPerfectsInCompetitiveRuns()
  {
    // Modern margins: 0 TooEarly, 1 Early, 2 EarlyPerfect, 3 Perfect-, 4 X-Perfect, 5 Perfect+,
    // 6 LatePerfect, 7 Late, 8 TooLate, 10 Miss, 11 Overload.
    var timeline = RunJudgmentTimelineBuilder.Build(
      "run",
      "failed",
      null,
      9_000_000,
      RunJudgmentSystem.ModernCompetitive,
      new[] { Hit(0, 1_000), Hit(3, 2_000), Hit(4, 3_000), Hit(5, 4_000), Hit(6, 5_000), Hit(10, 6_500_400) }
    );
    Assert(
      string.Join(",", timeline.Kinds) == "TooEarly,LatePerfect,Miss",
      "Judgment timeline kept a Perfect-family judgment or dropped a mistake."
    );
    Assert(string.Join(",", timeline.TimesMs) == "1,5,6500", "Judgment timeline times are not milliseconds.");
  }

  private static void LeavesOutPerfectsInClassicRuns()
  {
    var timeline = RunJudgmentTimelineBuilder.Build(
      "run",
      "failed",
      null,
      null,
      RunJudgmentSystem.ModernClassic,
      new[] { Hit(3, 1_000), Hit(4, 2_000), Hit(5, 3_000), Hit(7, 4_000) }
    );
    Assert(string.Join(",", timeline.Kinds) == "Late", "Classic Perfect judgments reached the judgment timeline.");
  }

  private static void MatchesTheReplayTimelineLength()
  {
    var hits = new[] { Hit(1, 2_000_000), Hit(7, 12_000_000) };
    var cleared = RunJudgmentTimelineBuilder.Build(
      "run",
      "Cleared",
      10_000_000,
      15_000_000,
      RunJudgmentSystem.ModernClassic,
      hits
    );
    Assert(cleared.DurationMs == 10_000, "A clear's timeline should end at its clear time.");
    Assert(cleared.TimesMs[1] == 10_000, "Judgments after the clear time should sit at the end.");

    var failed = RunJudgmentTimelineBuilder.Build(
      "run",
      "failed",
      null,
      11_000_000,
      RunJudgmentSystem.ModernClassic,
      hits
    );
    Assert(failed.DurationMs == 12_000, "A failed run's timeline should reach its last judgment.");
  }

  private static ReplayHitContext Hit(int margin, long timeUs) =>
    new ReplayHitContext(0, 0, 0, false, false, false, 0, 0, false, false, 0, margin, timeUs);
}
