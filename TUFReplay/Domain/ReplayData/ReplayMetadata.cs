using TUFHelper.ModScripts.Json;

namespace TUFReplay.Domain.ReplayData;

public class ReplayMetadata
{
  public int formatVersion;
  public int tufLevelId;
  public LevelListInfoElementJson levelInfo;
  public double? gameplayStartSongPosition;
  public bool? noFailMode;
  public int? levelPitchPercent;
  public float? pitchSpeedMultiplier;
  public float? effectivePitch;
  public string pitchSource;
  public string inputKeySpace;
  public string inputNativePlatform;
}
