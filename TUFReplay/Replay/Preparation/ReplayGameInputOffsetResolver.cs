using System;
using System.Collections.Generic;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Preparation;

internal static class ReplayGameInputOffsetResolver
{
  private const int MaximumIndexShift = 32;
  private const long MaximumOffsetUs = 300_000L;
  private const long MaximumMedianDeviationUs = 25_000L;
  private const int MinimumPairCount = 8;

  internal static int? Resolve(
    ReplayMetadata metadata,
    IReadOnlyList<RecordedInput> inputs,
    IReadOnlyList<ReplayHitContext> hitContexts,
    out bool inferred
  )
  {
    inferred = false;
    if (metadata?.gameInputOffsetMs is int stored)
      return stored;

    var downs = new List<long>();
    if (inputs != null)
    {
      for (int index = 0; index < inputs.Count; index++)
      {
        if (inputs[index].Down)
          downs.Add(inputs[index].TimeUs);
      }
    }

    var manualHits = new List<long>();
    if (hitContexts != null)
    {
      for (int index = 0; index < hitContexts.Count; index++)
      {
        if (!hitContexts[index].IsAuto)
          manualHits.Add(hitContexts[index].TimeUs);
      }
    }

    Candidate best = default;
    for (int shift = -MaximumIndexShift; shift <= MaximumIndexShift; shift++)
    {
      var offsets = new List<long>();
      int availablePairs = 0;
      for (int hitIndex = 0; hitIndex < manualHits.Count; hitIndex++)
      {
        int inputIndex = hitIndex + shift;
        if (inputIndex < 0 || inputIndex >= downs.Count)
          continue;
        availablePairs++;
        long offsetUs = downs[inputIndex] - manualHits[hitIndex];
        if (Math.Abs(offsetUs) <= MaximumOffsetUs)
          offsets.Add(offsetUs);
      }

      if (offsets.Count < MinimumPairCount || offsets.Count * 2 < availablePairs)
        continue;

      long medianUs = Median(offsets);
      var deviations = new List<long>(offsets.Count);
      for (int index = 0; index < offsets.Count; index++)
        deviations.Add(Math.Abs(offsets[index] - medianUs));
      long medianDeviationUs = Median(deviations);
      if (medianDeviationUs > MaximumMedianDeviationUs)
        continue;

      var candidate = new Candidate(offsets.Count, medianUs, medianDeviationUs);
      if (candidate.IsBetterThan(best))
        best = candidate;
    }

    if (!best.Valid)
      return null;

    inferred = true;
    return (int)Math.Round(best.MedianUs / 1000d, MidpointRounding.AwayFromZero);
  }

  private static long Median(List<long> values)
  {
    values.Sort();
    int middle = values.Count / 2;
    return (values.Count & 1) != 0 ? values[middle] : (values[middle - 1] + values[middle]) / 2L;
  }

  private readonly struct Candidate
  {
    internal readonly int PairCount;
    internal readonly long MedianUs;
    internal readonly long MedianDeviationUs;

    internal Candidate(int pairCount, long medianUs, long medianDeviationUs)
    {
      PairCount = pairCount;
      MedianUs = medianUs;
      MedianDeviationUs = medianDeviationUs;
    }

    internal bool Valid => PairCount > 0;

    internal bool IsBetterThan(Candidate other) =>
      PairCount > other.PairCount
      || (PairCount == other.PairCount && MedianDeviationUs < other.MedianDeviationUs);
  }
}
