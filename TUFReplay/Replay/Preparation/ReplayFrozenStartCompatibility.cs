using System;
using System.Collections.Generic;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Preparation;

internal static class ReplayFrozenStartCompatibility
{
  private const long MinimumCorrectionUs = 100_000L;
  private const long MaximumCorrectionUs = 10_000_000L;
  private const long InitialHitToleranceUs = 50_000L;
  private const long FollowUpPairToleranceUs = 100_000L;

  public static long DetectCorrectionUs(
    int startTile,
    IReadOnlyList<RecordedInput> inputs,
    IReadOnlyList<ReplayHitContext> hitContexts
  )
  {
    if (startTile <= 1 || inputs == null || hitContexts == null)
      return 0L;

    int firstDownIndex = FindDownInput(inputs, 0);
    int firstManualHitIndex = FindManualHit(hitContexts, 0);
    if (firstDownIndex < 0 || firstManualHitIndex < 0)
      return 0L;

    long firstInputTimeUs = inputs[firstDownIndex].TimeUs;
    long firstHitTimeUs = hitContexts[firstManualHitIndex].TimeUs;
    long correctionUs = firstInputTimeUs - firstHitTimeUs;
    if (
      firstHitTimeUs > InitialHitToleranceUs
      || correctionUs < MinimumCorrectionUs
      || correctionUs > MaximumCorrectionUs
    )
      return 0L;

    int secondDownIndex = FindDownInput(inputs, firstDownIndex + 1);
    int secondManualHitIndex = FindManualHit(hitContexts, firstManualHitIndex + 1);
    if (
      secondDownIndex >= 0
      && secondManualHitIndex >= 0
      && Math.Abs(inputs[secondDownIndex].TimeUs - hitContexts[secondManualHitIndex].TimeUs)
        > FollowUpPairToleranceUs
    )
      return 0L;

    return correctionUs;
  }

  public static List<RecordedInput> ShiftInputs(IReadOnlyList<RecordedInput> inputs, long correctionUs)
  {
    var shifted = new List<RecordedInput>(inputs?.Count ?? 0);
    if (inputs == null)
      return shifted;
    for (int i = 0; i < inputs.Count; i++)
    {
      RecordedInput input = inputs[i];
      shifted.Add(
        new RecordedInput(input.TimeUs - correctionUs, input.Key, input.Flags, input.NativeCode, input.NativeFlags)
      );
    }
    return shifted;
  }

  public static List<ReplayHitContext> ShiftHitContexts(
    IReadOnlyList<ReplayHitContext> hitContexts,
    long correctionUs
  )
  {
    var shifted = new List<ReplayHitContext>(hitContexts?.Count ?? 0);
    if (hitContexts == null)
      return shifted;
    for (int i = 0; i < hitContexts.Count; i++)
    {
      ReplayHitContext hit = hitContexts[i];
      shifted.Add(
        new ReplayHitContext(
          hit.CurrentFloorID,
          hit.CurrAngle,
          hit.OverloadCounter,
          hit.NoFailHit,
          hit.IsAuto,
          hit.NextFloorAuto,
          hit.CachedAngle,
          hit.TargetExitAngle,
          hit.MidspinInfiniteMargin,
          hit.RDCAuto,
          hit.CurFreeRoamSection,
          hit.ResolvedHitMargin,
          ShiftNonNegativeTime(hit.TimeUs, correctionUs)
        )
      );
    }
    return shifted;
  }

  public static long ShiftNonNegativeTime(long timeUs, long correctionUs) => Math.Max(0L, timeUs - correctionUs);

  private static int FindDownInput(IReadOnlyList<RecordedInput> inputs, int startIndex)
  {
    for (int i = Math.Max(0, startIndex); i < inputs.Count; i++)
    {
      if (inputs[i].Down)
        return i;
    }
    return -1;
  }

  private static int FindManualHit(IReadOnlyList<ReplayHitContext> hitContexts, int startIndex)
  {
    for (int i = Math.Max(0, startIndex); i < hitContexts.Count; i++)
    {
      if (!hitContexts[i].IsAuto)
        return i;
    }
    return -1;
  }
}
