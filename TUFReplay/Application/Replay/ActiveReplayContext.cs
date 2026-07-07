using System.Collections.Generic;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;

namespace TUFReplay.Application.Replay;

public class ActiveReplayContext
{
  public int TufLevelId;
  public string OpenedAtUtc;
  public List<RecordedInput> Inputs;
  public List<ReplayHitContext> HitContexts;
  public ReplayInputScheduler NativeInputScheduler;
  public ReplayNativeInputPlayer NativeInputPlayer;
  public ReplayHitContextPlayer HitContextPlayer;
  public ReplayMetadata Meta;
  public bool RunStarted;
}
