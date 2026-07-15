namespace TUFReplay.Domain.Activity;

public class SegmentGroupSummary
{
  public int SegmentGroupIndex;
  public int StartTile;
  public int AttemptCount;
  public int BestLastTile;
  public string FirstStartedAtUtc;
  public string LastStartedAtUtc;
}

public class ActivityDaySummary
{
  public string Date;
  public int AppSessionCount;
  public int LevelSessionCount;
  public int RunCount;
  public int NoFailRunCount;
  public int UniqueLevelCount;
  public string StartedAtUtc;
  public string EndedAtUtc;
}

public class LevelSessionOverview
{
  public string Id;
  public string AppSessionId;
  public int? TufLevelId;
  public string LevelPath;
  public string OpenedAtUtc;
  public string ClosedAtUtc;
  public int LevelTileCount;
  public int RunCount;
  public int ClearRunCount;
  public int NoFailRunCount;
  public int? FirstStartTile;
  public int? LastStartTile;
  public bool ChartAvailable;
  public string Song;
  public string Author;
  public string Artist;
  public LevelMetadataState MetadataState;
}
