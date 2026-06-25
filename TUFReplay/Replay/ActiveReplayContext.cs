using System.Collections.Generic;
using TUFReplay.Shared;

namespace TUFReplay.Replay;

public class ActiveReplayContext
{
  public string RecordId;
  public int TufLevelId;
  public PlayRecord Record;
  public string OpenedAtUtc;
  public List<ReplayInputEvent> Inputs;
  public List<ReplayHitContext> HitContexts;
  public ReplayInputScheduler NativeInputScheduler;
  public ReplayNativeInputPlayer NativeInputPlayer;
  public ReplayHitContextPlayer HitContextPlayer;
  public ReplayRecordMeta Meta;
  public bool RunStarted;
}
