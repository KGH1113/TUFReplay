namespace TUFReplay.Activity.Models;

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
  public RunJudgmentSystem JudgmentSystem;
  public JudgmentCounts JudgmentCounts = new JudgmentCounts();
  public byte[] GameplayHash;
  public int? GameplayHashVersion;

  public int InputCount;
  public int HitContextCount;
  public long InputCsvBytes;
  public long HitContextCsvBytes;
  public long MicrophoneRecordingBytes;
  public int? MicrophoneSampleRate;
  public int? MicrophoneChannels;
  public long? MicrophoneFrameCount;
  public bool MicrophoneRecordingPermanent;
  public string MicrophoneRecordingExpiresAtUtc;
  public long CameraRecordingBytes;
  public bool CameraRecordingPermanent;
  public string CameraRecordingExpiresAtUtc;
  public bool ReplayPlayable;
  public string ReplayUnavailableReason;
  public bool Locked;
  public TUFReplay.Replay.Models.ReplayArtifact ReplayArtifact;

  /// <summary>
  /// A full run from tile zero; a No-Fail run counts only without a Miss or Overload.
  /// Same rule as <c>ActivityRepository.ClearRunCondition</c> and the web's <c>runOutcome</c>.
  /// </summary>
  public bool IsClear =>
    (
      string.Equals(Result, "cleared", System.StringComparison.OrdinalIgnoreCase)
      || string.Equals(Result, "completed", System.StringComparison.OrdinalIgnoreCase)
    )
    && StartTile == 0
    && (!NoFailMode || (JudgmentCounts != null && JudgmentCounts.Miss == 0 && JudgmentCounts.Overload == 0));

  /// <summary>Mod states captured for this run, saved in the same transaction.</summary>
  internal TUFReplay.ModApi.Models.CapturedRunStates RunStates;
}
