using System.Collections.Generic;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;

namespace TUFReplay.Application.Replay;

public class ActiveReplayContext
{
  public string OperationId;
  public string RunId;
  public string LevelPath;
  public string Result;
  public int? TufLevelId;
  public int StartTile;
  public long TerminalTimeUs;
  public string OpenedAtUtc;
  public List<RecordedInput> Inputs;
  public List<ReplayHitContext> HitContexts;
  public ReplayInputScheduler NativeInputScheduler;
  public ReplayNativeInputPlayer NativeInputPlayer;
  public ReplayHitContextPlayer HitContextPlayer;
  public ReplayMetadata Meta;
  public bool RunStarted;
  public bool WonClockStarted;
  public double WonClockStartedAt;
  public long WonClockStartTimeUs;
  public int? OriginalLevelPitchPercent;
  public bool ReplayPitchApplied;
}
