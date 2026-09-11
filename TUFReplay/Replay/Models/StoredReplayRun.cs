using TUFReplay.Activity.Models;

namespace TUFReplay.Replay.Models;

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
  public string EngineId;
  public int FormatVersion;
  public string ReplayUnavailableReason;
  public byte[] GameplayHash;
  public int? GameplayHashVersion;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public bool NoFailMode;
}
