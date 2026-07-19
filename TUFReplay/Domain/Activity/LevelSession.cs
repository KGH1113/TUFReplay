namespace TUFReplay.Domain.Activity;

public class LevelSession
{
  public string Id;
  public string AppSessionId;
  public int? TufLevelId;
  public string LevelPath;
  public string OpenedAtUtc;
  public string ClosedAtUtc;
  public int LevelTileCount;
  public byte[] LevelFileHash;
  public string Song;
  public string Author;
  public string Artist;
  public LevelMetadataState MetadataState;
}

public sealed class LevelMetadataSnapshot
{
  public string Song;
  public string Author;
  public string Artist;
}

public enum LevelMetadataState
{
  Pending = 0,
  Captured = 1,
  Unavailable = 2,
}
