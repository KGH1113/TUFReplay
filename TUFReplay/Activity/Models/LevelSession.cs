namespace TUFReplay.Activity.Models;

public class LevelSession
{
  public string Id;
  public string LevelId;
  public string AppSessionId;
  public LevelSourceKind SourceKind;
  public int? TufLevelId;
  public string LevelPath;
  public string OpenedAtUtc;
  public string ClosedAtUtc;
  public int LevelTileCount;
  public byte[] GameplayHash;
  public int? GameplayHashVersion;
  public string Song;
  public string Author;
  public string Artist;
  public LevelMetadataState MetadataState;
}

public sealed class LevelRecord
{
  public string Id;
  public string IdentityKey;
  public LevelSourceKind SourceKind;
  public int? TufLevelId;
  public string LevelPath;
  public int LevelTileCount;
  public byte[] GameplayHash;
  public int? GameplayHashVersion;
  public string Song;
  public string Author;
  public string Artist;
  public LevelMetadataState MetadataState;
  public string FirstSeenAtUtc;
  public string LastSeenAtUtc;
}

public enum LevelSourceKind
{
  Local = 0,
  Tuf = 1,
}

public sealed class LogicalLevelOverview
{
  public string Id;
  public int? TufLevelId;
  public string Song;
  public string Author;
  public string Artist;
  public string FirstSeenAtUtc;
  public string LastSeenAtUtc;
  public int LevelTileCount;
  public int VisitCount;
  public int RunCount;
  public int ClearRunCount;
  public int NoFailRunCount;
  public int? FirstStartTile;
  public int? LastStartTile;
  public bool ChartAvailable;
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
