namespace TUFReplay.Replay.Models;

public class ReplayMetadata
{
  public int metadataVersion;
  public int? tufLevelId;
  public double? gameplayStartSongPosition;
  public long? wonTimeUs;
  public long? terminalTimeUs;
  public bool? noFailMode;
  public int? levelPitchPercent;
  public float? pitchSpeedMultiplier;
  public float? effectivePitch;
  public string judgmentSystem;
  public string pitchSource;
  public string inputTimeBase;
  public string inputFormat;
  public string inputCapture;
  public string inputKeySpace;
  public string inputNativePlatform;
}
