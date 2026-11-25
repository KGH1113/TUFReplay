namespace TUFReplay.Activity.Charts;

/// <summary>Decides whether a chart's hashes match the hashes stored for a recorded level.</summary>
public static class GameplayChartHashMatch
{
  /// <summary>
  /// V5 decides when the record has it. A record from before V5 only has V4, which
  /// still matches the same file, but rejects charts that differ in V5-ignored fields.
  /// </summary>
  public static bool Matches(byte[] storedV4, int? storedV4Version, byte[] storedV5, GameplayChartHashes candidate)
  {
    if (candidate == null)
      return false;
    if (storedV5 != null)
      return GameplayChartHash.IsV5(storedV5) && GameplayChartHash.Equals(storedV5, candidate.V5);
    return GameplayChartHash.IsSupported(storedV4Version, storedV4) && GameplayChartHash.Equals(storedV4, candidate.V4);
  }

  /// <summary>
  /// True when a record without V5 can take it from this chart: the chart is the
  /// recorded file and its V4 still matches. Only the recorded file may supply it,
  /// because V5 reads fields V4 never saw.
  /// </summary>
  public static bool CanTakeV5(byte[] storedV4, int? storedV4Version, byte[] storedV5, GameplayChartHashes original) =>
    storedV5 == null
    && original != null
    && GameplayChartHash.IsV5(original.V5)
    && GameplayChartHash.IsSupported(storedV4Version, storedV4)
    && GameplayChartHash.Equals(storedV4, original.V4);
}
