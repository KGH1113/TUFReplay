namespace TUFReplay.Domain.Activity;

public class RunRecord
{
  public string Id;
  public string AppSessionId;
  public string LevelSessionId;
  public int? TufLevelId;
  public int RunIndex;
  public int SegmentGroupIndex;

  public string StartedAtUtc;
  public string EndedAtUtc;

  public int LevelTileCount;
  public int StartTile;
  public int? LastTile;

  public string Result;
  public bool NoFailMode;

  public double? GameplayStartSongPosition;
  public int? LevelPitchPercent;
  public float? EffectivePitch;
  public float? XAccuracy;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public JudgmentCounts JudgmentCounts = new JudgmentCounts();
  public byte[] GameplayHash;
  public int? GameplayHashVersion;

  public int InputCount;
  public int HitContextCount;
  public byte[] InputCsv;
  public byte[] HitContextCsv;
  public long InputCsvBytes;
  public long HitContextCsvBytes;
  public long MicrophoneRecordingBytes;
  public int? MicrophoneSampleRate;
  public int? MicrophoneChannels;
  public long? MicrophoneFrameCount;
  public string MetaJson;
}
