using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.Compatibility;

namespace TUFReplay.Recording.Telemetry;

internal static class RecordingRuntimeTelemetry
{
  internal static int GetLevelTileCount()
  {
    try
    {
      if (ADOBase.lm?.listFloors != null)
        return ADOBase.lm.listFloors.Count;
    }
    catch
    {
      // Best-effort telemetry; run recording still works with a zero tile count.
    }

    return 0;
  }

  internal static int GetCurrentTile()
  {
    try
    {
      if (ADOBase.controller?.currFloor != null)
        return Math.Max(0, ADOBase.controller.currFloor.seqID);
      if (scrController.instance?.currFloor != null)
        return Math.Max(0, scrController.instance.currFloor.seqID);
    }
    catch
    {
      // Best-effort telemetry; callers have safe fallbacks.
    }

    return 0;
  }

  internal static bool IsNoFailModeActive()
  {
    try
    {
      return GCS.useNoFail || (ADOBase.controller != null && ADOBase.controller.noFail);
    }
    catch
    {
      return false;
    }
  }

  internal static int? GetLevelPitchPercent()
  {
    try
    {
      if (ADOBase.isLevelEditor && ADOBase.editor != null)
      {
        return ADOBase.editor.levelData?.pitch;
      }

      return ADOBase.customLevel?.levelData?.pitch;
    }
    catch
    {
      return null;
    }
  }

  internal static float? GetPitchSpeedMultiplier()
  {
    try
    {
      if (ADOBase.isLevelEditor && ADOBase.editor != null)
      {
        return ADOBase.editor.playbackSpeed;
      }

      return GCS.speedTrialMode ? GCS.currentSpeedTrial : 1f;
    }
    catch
    {
      return null;
    }
  }

  internal static float? GetEffectivePitch()
  {
    try
    {
      if (ADOBase.conductor == null || ADOBase.conductor.song == null)
        return null;
      return ADOBase.conductor.song.pitch;
    }
    catch
    {
      return null;
    }
  }

  internal static float? GetXAccuracy()
  {
    try
    {
      float value = ADOBase.controller?.playerOne?.marginTracker?.percentXAcc ?? float.NaN;
      if (float.IsNaN(value) || float.IsInfinity(value))
        return null;
      return Math.Max(0f, Math.Min(1f, value));
    }
    catch
    {
      return null;
    }
  }

  internal static void CaptureJudgmentStats(RecordedRunPayload data)
  {
    try
    {
      scrMarginTracker tracker = ADOBase.controller?.playerOne?.marginTracker;
      if (tracker == null)
        return;

      int[] hits = tracker.hitMarginsCount;
      if (hits == null)
        return;

      RunJudgmentSystem judgmentSystem = data.JudgmentSystem;
      int auto = AdofaiRuntimeCompatibility.ReadHitCount(hits, "Auto");
      int perfectMinus = AdofaiRuntimeCompatibility.ReadHitCount(hits, "PerfectMinus");
      int xPerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "XPerfect");
      int perfectPlus = AdofaiRuntimeCompatibility.ReadHitCount(hits, "PerfectPlus");

      data.JudgmentCounts = new JudgmentCounts
      {
        Overload = AdofaiRuntimeCompatibility.ReadHitCount(hits, "FailOverload"),
        TooEarly = AdofaiRuntimeCompatibility.ReadHitCount(hits, "TooEarly"),
        Early = AdofaiRuntimeCompatibility.ReadHitCount(hits, "VeryEarly"),
        EarlyPerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "EarlyPerfect"),
        Perfect =
          judgmentSystem == RunJudgmentSystem.Legacy ? AdofaiRuntimeCompatibility.ReadHitCount(hits, "Perfect") + auto
          : judgmentSystem == RunJudgmentSystem.ModernClassic ? perfectMinus + xPerfect + perfectPlus + auto
          : 0,
        PerfectMinus = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? perfectMinus : 0,
        XPerfect = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? xPerfect + auto : 0,
        PerfectPlus = judgmentSystem == RunJudgmentSystem.ModernCompetitive ? perfectPlus : 0,
        LatePerfect = AdofaiRuntimeCompatibility.ReadHitCount(hits, "LatePerfect"),
        Late = AdofaiRuntimeCompatibility.ReadHitCount(hits, "VeryLate"),
        TooLate = AdofaiRuntimeCompatibility.ReadHitCount(hits, "TooLate"),
        Miss = AdofaiRuntimeCompatibility.ReadHitCount(hits, "FailMiss"),
      };
    }
    catch
    {
      // Judgment telemetry must never interrupt run persistence.
    }
  }

  internal static RunJudgmentDifficulty? GetCurrentJudgmentDifficulty()
  {
    try
    {
      int difficulty = (int)GCS.difficulty;
      if (difficulty < (int)RunJudgmentDifficulty.Lenient || difficulty > (int)RunJudgmentDifficulty.Strict)
        return null;
      return (RunJudgmentDifficulty)difficulty;
    }
    catch
    {
      return null;
    }
  }
}
