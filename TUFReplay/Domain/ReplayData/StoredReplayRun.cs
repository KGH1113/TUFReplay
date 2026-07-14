namespace TUFReplay.Domain.ReplayData;

public sealed class StoredReplayRun
{
  public string Id;
  public string LevelSessionId;
  public int? TufLevelId;
  public string LevelPath;
  public int LevelTileCount;
  public int StartTile;
  public int? LastTile;
  public string Result;
  public byte[] InputCsv;
  public byte[] HitContextCsv;
  public string MetaJson;
}
