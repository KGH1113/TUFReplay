using TUFReplay.Shared;

namespace TUFReplay.Replay;

public class ActiveReplayContext
{
  public string RecordId;
  public int TufLevelId;
  public PlayRecord Record;
  public string OpenedAtUtc;
}
