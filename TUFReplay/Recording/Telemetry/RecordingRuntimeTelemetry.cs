using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;


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

      data.JudgmentCounts = new JudgmentCounts
      {
        Overload = ReadHitCount(hits, HitMargin.FailOverload),
        TooEarly = ReadHitCount(hits, HitMargin.TooEarly),
        Early = ReadHitCount(hits, HitMargin.VeryEarly),
        EarlyPerfect = ReadHitCount(hits, HitMargin.EarlyPerfect),
        Perfect = ReadHitCount(hits, HitMargin.Perfect) + ReadHitCount(hits, HitMargin.Auto),
        LatePerfect = ReadHitCount(hits, HitMargin.LatePerfect),
        Late = ReadHitCount(hits, HitMargin.VeryLate),
        TooLate = ReadHitCount(hits, HitMargin.TooLate),
        Miss = ReadHitCount(hits, HitMargin.FailMiss),
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

  internal static int ReadHitCount(int[] hits, HitMargin margin)
  {
    int index = (int)margin;
    return index >= 0 && index < hits.Length ? Math.Max(0, hits[index]) : 0;
  }

}
