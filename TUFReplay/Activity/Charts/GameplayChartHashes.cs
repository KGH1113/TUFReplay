namespace TUFReplay.Activity.Charts;

/// <summary>
/// The two gameplay hashes of one chart. V4 is kept so runs recorded before V5
/// still match; V5 decides matching once a run carries it.
/// </summary>
public sealed class GameplayChartHashes
{
  public GameplayChartHashes(byte[] v4, byte[] v5)
  {
    V4 = v4;
    V5 = v5;
  }

  public byte[] V4 { get; }
  public byte[] V5 { get; }
}
