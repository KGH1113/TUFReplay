using System.Collections.Generic;
using TUFReplay.Activity.Models;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.Sessions;

public class ActiveReplayContext
{
  public string OperationId;
  public string RunId;
  public string LevelPath;
  public byte[] GameplayHash;
  public int GameplayHashVersion;
  public string Result;
  public int? TufLevelId;
  public int StartTile;
  public RunJudgmentDifficulty? JudgmentDifficulty;
  public bool NoFailMode;
  public long TerminalTimeUs;
  public string OpenedAtUtc;
  public List<RecordedInput> Inputs;
  public List<ReplayHitContext> HitContexts;
  public ReplayInputScheduler NativeInputScheduler;
  public ReplayNativeInputPlayer NativeInputPlayer;
  public ReplayHitContextPlayer HitContextPlayer;
  public IReplayMicrophonePlayer MicrophonePlayer;
  public ReplayMetadata Meta;
  public ReplayPlaybackPhase Phase = ReplayPlaybackPhase.Prepared;
  public bool RunStarted;
  public bool WonClockStarted;
  public double WonClockStartedAt;
  public long WonClockStartTimeUs;
  public int? OriginalLevelPitchPercent;
  public bool ReplayPitchApplied;
  public int? OriginalJudgmentDifficulty;
  public bool ReplayJudgmentDifficultyApplied;
  public bool? OriginalNoFailMode;
  public bool ReplayNoFailApplied;
}
