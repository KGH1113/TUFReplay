using TUFHelper.ModScripts.Json;

namespace TUFReplay.Replay;

public class ReplayRecordMeta
{
  public int formatVersion;
  public int tufLevelId;
  public LevelListInfoElementJson levelInfo;
  public double? gameplayStartSongPosition;
}
