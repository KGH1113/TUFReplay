namespace TUFReplay.Domain.ReplayData;

public class ReplayMetadata
{
  public int formatVersion;
  public int? tufLevelId;
  public double? gameplayStartSongPosition;
  public long? wonTimeUs;
  public long? terminalTimeUs;
  public bool? noFailMode;
  public int? levelPitchPercent;
  public float? pitchSpeedMultiplier;
  public float? effectivePitch;
  public string pitchSource;
  public string inputTimeBase;
  public string inputKeySpace;
  public string inputNativePlatform;
}
