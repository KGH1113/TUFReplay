using System;

namespace TUFReplay.Replay.Timeline;

internal enum ReplayTimelineJudgmentKind
{
  Overload,
  TooEarly,
  Early,
  EarlyPerfect,
  Perfect,
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
  internal static bool TryMapHitMargin(HitMargin hitMargin, out ReplayTimelineJudgmentKind kind)
  {
    return TryMapHitMarginValue((int)hitMargin, out kind);
  }

  internal static bool TryMapHitMarginValue(int hitMarginValue, out ReplayTimelineJudgmentKind kind)
  {
    switch (hitMarginValue)
    {
      case (int)HitMargin.FailOverload:
        kind = ReplayTimelineJudgmentKind.Overload;
        return true;
      case (int)HitMargin.TooEarly:
        kind = ReplayTimelineJudgmentKind.TooEarly;
        return true;
      case (int)HitMargin.VeryEarly:
        kind = ReplayTimelineJudgmentKind.Early;
        return true;
      case (int)HitMargin.EarlyPerfect:
        kind = ReplayTimelineJudgmentKind.EarlyPerfect;
        return true;
      case (int)HitMargin.Perfect:
      case (int)HitMargin.Auto:
        kind = ReplayTimelineJudgmentKind.Perfect;
        return true;
      case (int)HitMargin.LatePerfect:
        kind = ReplayTimelineJudgmentKind.LatePerfect;
        return true;
      case (int)HitMargin.VeryLate:
        kind = ReplayTimelineJudgmentKind.Late;
        return true;
      case (int)HitMargin.TooLate:
        kind = ReplayTimelineJudgmentKind.TooLate;
        return true;
      case (int)HitMargin.FailMiss:
        kind = ReplayTimelineJudgmentKind.Miss;
        return true;
      default:
        kind = default;
        return false;
    }
  }

  internal static bool TryEstimateLegacyTimeUs(
    double targetSongTime,
    double gameplayStartSongPosition,
    double currAngle,
    double bpm,
    double speed,
    double pitch,
    long durationTimeUs,
    out long timeUs
  )
  {
    timeUs = 0L;
    if (
      !IsFinite(targetSongTime)
      || !IsFinite(gameplayStartSongPosition)
      || !IsFinite(currAngle)
      || !IsFinite(bpm)
      || !IsFinite(speed)
      || !IsFinite(pitch)
      || bpm <= 0d
      || speed <= 0d
      || pitch <= 0d
      || durationTimeUs < 0L
    )
      return false;

    double crotchet = 60d / (bpm * speed);
    double angleOffset = currAngle * crotchet / (Math.PI * pitch);
    double estimatedTimeUs = (targetSongTime + angleOffset - gameplayStartSongPosition) * 1_000_000d;
    if (!IsFinite(estimatedTimeUs) || estimatedTimeUs <= long.MinValue || estimatedTimeUs >= long.MaxValue)
      return false;

    timeUs = Math.Max(0L, Math.Min((long)estimatedTimeUs, durationTimeUs));
    return true;
  }

  private static bool IsFinite(double value)
  {
    return !double.IsNaN(value) && !double.IsInfinity(value);
  }
}
