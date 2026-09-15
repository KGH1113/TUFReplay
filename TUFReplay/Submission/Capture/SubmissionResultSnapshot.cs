using System;
using System.Collections.Generic;
using TUFReplay.Activity.Models;

namespace TUFReplay.Submission.Capture;

/// <summary>Immutable result values read once at the winning-hit boundary.</summary>
public sealed class SubmissionResultSnapshot
{
  public int version = 1;
  public int[] judgments;
  public int perfectMinus;
  public int perfectPlus;
  public int adofaiVersion;
  public bool isXPerfectMode;
  public bool isNoHoldTap;

  private SubmissionResultSnapshot(
    int[] judgments,
    int perfectMinus,
    int perfectPlus,
    int adofaiVersion,
    bool isXPerfectMode,
    bool isNoHoldTap
  )
  {
    this.judgments = judgments;
    this.perfectMinus = perfectMinus;
    this.perfectPlus = perfectPlus;
    this.adofaiVersion = adofaiVersion;
    this.isXPerfectMode = isXPerfectMode;
    this.isNoHoldTap = isNoHoldTap;
  }

  public static bool TryCreate(
    IReadOnlyDictionary<string, int> hitCounts,
    RunJudgmentSystem judgmentSystem,
    string gameVersion,
    int holdBehavior,
    out SubmissionResultSnapshot snapshot
  )
  {
    snapshot = null;
    if (
      hitCounts == null
      || !TryParseAdofaiVersion(gameVersion, out int adofaiVersion)
      || holdBehavior < 0
      || holdBehavior > 2
    )
      return false;

    bool isXPerfectMode = judgmentSystem == RunJudgmentSystem.ModernCompetitive;
    if (isXPerfectMode && adofaiVersion != 3)
      return false;

    if (
      !TryRead(hitCounts, "FailOverload", optional: false, out int overload)
      || !TryRead(hitCounts, "TooEarly", optional: false, out int tooEarly)
      || !TryRead(hitCounts, "VeryEarly", optional: false, out int early)
      || !TryRead(hitCounts, "EarlyPerfect", optional: false, out int earlyPerfect)
      || !TryRead(hitCounts, "LatePerfect", optional: false, out int latePerfect)
      || !TryRead(hitCounts, "VeryLate", optional: false, out int late)
      || !TryRead(hitCounts, "TooLate", optional: false, out int tooLate)
      || !TryRead(hitCounts, "FailMiss", optional: false, out int miss)
      || !TryRead(hitCounts, "Auto", optional: true, out int auto)
    )
      return false;

    int perfect;
    int perfectMinus = 0;
    int perfectPlus = 0;
    switch (judgmentSystem)
    {
      case RunJudgmentSystem.Legacy:
        if (!TryRead(hitCounts, "Perfect", optional: false, out int legacyPerfect))
          return false;
        perfect = checked(legacyPerfect + auto);
        break;
      case RunJudgmentSystem.ModernClassic:
      case RunJudgmentSystem.ModernCompetitive:
        if (
          !TryRead(hitCounts, "PerfectMinus", optional: false, out int minus)
          || !TryRead(hitCounts, "XPerfect", optional: false, out int xPerfect)
          || !TryRead(hitCounts, "PerfectPlus", optional: false, out int plus)
        )
          return false;
        perfect = checked(xPerfect + auto);
        if (isXPerfectMode)
        {
          perfectMinus = minus;
          perfectPlus = plus;
        }
        else
          perfect = checked(perfect + minus + plus);
        break;
      default:
        return false;
    }

    snapshot = new SubmissionResultSnapshot(
      new[] { overload, tooEarly, early, earlyPerfect, perfect, latePerfect, late, tooLate, miss },
      perfectMinus,
      perfectPlus,
      adofaiVersion,
      isXPerfectMode,
      isNoHoldTap: holdBehavior != 0
    );
    return true;
  }

  private static bool TryRead(
    IReadOnlyDictionary<string, int> hitCounts,
    string marginName,
    bool optional,
    out int count
  )
  {
    count = 0;
    if (!hitCounts.TryGetValue(marginName, out int value))
      return optional;

    if (value < 0)
      return false;

    count = value;
    return true;
  }

  private static bool TryParseAdofaiVersion(string value, out int adofaiVersion)
  {
    adofaiVersion = 0;
    if (string.IsNullOrWhiteSpace(value))
      return false;

    string version = value.Trim();
    if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
      version = version.Substring(1);
    string[] parts = version.Split('.');
    if (
      parts.Length < 3
      || !TryLeadingDigits(parts[0], out int major)
      || !TryLeadingDigits(parts[1], out int minor)
      || !TryLeadingDigits(parts[2], out int patch)
    )
      return false;

    if (major == 2)
    {
      adofaiVersion = 1;
      return true;
    }
    if (major == 3)
    {
      adofaiVersion = (minor, patch).CompareTo((4, 0)) >= 0 ? 3 : 2;
      return true;
    }
    return false;
  }

  private static bool TryLeadingDigits(string value, out int number)
  {
    number = 0;
    int digitCount = 0;
    while (digitCount < value.Length && char.IsDigit(value[digitCount]))
      digitCount++;
    return digitCount > 0 && int.TryParse(value.Substring(0, digitCount), out number);
  }
}
