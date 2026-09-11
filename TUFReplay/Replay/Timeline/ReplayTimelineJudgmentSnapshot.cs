namespace TUFReplay.Replay.Timeline;

using TUFReplay.Activity.Models;

internal enum ReplayTimelineJudgmentKind
{
  Overload,
  TooEarly,
  Early,
  EarlyPerfect,
  Perfect,
  PerfectMinus,
  XPerfect,
  PerfectPlus,
  LatePerfect,
  Late,
  TooLate,
  Miss,
}

internal readonly struct ReplayTimelineJudgmentSnapshot
{
  public readonly long TimeUs;
  public readonly ReplayTimelineJudgmentKind Kind;

  public ReplayTimelineJudgmentSnapshot(long timeUs, ReplayTimelineJudgmentKind kind)
  {
    TimeUs = timeUs;
    Kind = kind;
  }
}

internal static class ReplayTimelineJudgmentMath
{
  internal static bool TryMapHitMargin(
    HitMargin hitMargin,
    RunJudgmentSystem judgmentSystem,
    out ReplayTimelineJudgmentKind kind
  )
  {
    return TryMapHitMarginValue((int)hitMargin, judgmentSystem, out kind);
  }

  internal static bool TryMapHitMarginValue(
    int hitMarginValue,
    RunJudgmentSystem judgmentSystem,
    out ReplayTimelineJudgmentKind kind
  )
  {
    if (judgmentSystem == RunJudgmentSystem.Legacy)
    {
      switch (hitMarginValue)
      {
        case 9:
          kind = ReplayTimelineJudgmentKind.Overload;
          return true;
        case 0:
          kind = ReplayTimelineJudgmentKind.TooEarly;
          return true;
        case 1:
          kind = ReplayTimelineJudgmentKind.Early;
          return true;
        case 2:
          kind = ReplayTimelineJudgmentKind.EarlyPerfect;
          return true;
        case 3:
        case 10:
          kind = ReplayTimelineJudgmentKind.Perfect;
          return true;
        case 4:
          kind = ReplayTimelineJudgmentKind.LatePerfect;
          return true;
        case 5:
          kind = ReplayTimelineJudgmentKind.Late;
          return true;
        case 6:
          kind = ReplayTimelineJudgmentKind.TooLate;
          return true;
        case 8:
          kind = ReplayTimelineJudgmentKind.Miss;
          return true;
        default:
          kind = default;
          return false;
      }
    }

    switch (hitMarginValue)
    {
      case 11:
        kind = ReplayTimelineJudgmentKind.Overload;
        return true;
      case 0:
        kind = ReplayTimelineJudgmentKind.TooEarly;
        return true;
      case 1:
        kind = ReplayTimelineJudgmentKind.Early;
        return true;
      case 2:
        kind = ReplayTimelineJudgmentKind.EarlyPerfect;
        return true;
      case 12:
        kind = ReplayTimelineJudgmentKind.Perfect;
        return true;
      case 3:
        kind = judgmentSystem == RunJudgmentSystem.ModernCompetitive
          ? ReplayTimelineJudgmentKind.PerfectMinus
          : ReplayTimelineJudgmentKind.Perfect;
        return true;
      case 4:
        kind = judgmentSystem == RunJudgmentSystem.ModernCompetitive
          ? ReplayTimelineJudgmentKind.XPerfect
          : ReplayTimelineJudgmentKind.Perfect;
        return true;
      case 5:
        kind = judgmentSystem == RunJudgmentSystem.ModernCompetitive
          ? ReplayTimelineJudgmentKind.PerfectPlus
          : ReplayTimelineJudgmentKind.Perfect;
        return true;
      case 6:
        kind = ReplayTimelineJudgmentKind.LatePerfect;
        return true;
      case 7:
        kind = ReplayTimelineJudgmentKind.Late;
        return true;
      case 8:
        kind = ReplayTimelineJudgmentKind.TooLate;
        return true;
      case 10:
        kind = ReplayTimelineJudgmentKind.Miss;
        return true;
      default:
        kind = default;
        return false;
    }
  }
}
