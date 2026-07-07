using System;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
using TUFReplay.Infrastructure.NativeInput;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Application.Replay;

public static class ReplaySessionService
{
  private static ActiveReplayContext _activeContext;
  private static string _lastNativeGateLogKey;
  private static int _lastNativeGateLogFrame = -100000;
  private static int _lastNativeTickLogFrame = -100000;
  private static int _lastHitContextTickLogFrame = -100000;
  private static int _pendingReplayPitchApplyFrame = -1;
  private static bool _suppressReplayMarkFail;

  public static bool HasActiveContext => _activeContext != null;
  public static bool UsesHitContextPlayback => _activeContext?.HitContextPlayer?.Count > 0;

  public static bool IsActiveReplayLevel(int tufLevelId)
  {
    return _activeContext != null && _activeContext.TufLevelId == tufLevelId;
  }

  public static void ClearActiveContextIfLevelChanged(int? tufLevelId)
  {
    if (_activeContext == null) return;

    if (!tufLevelId.HasValue)
    {
      StopActiveReplay("tuf_level_id_missing");
      return;
    }

    if (_activeContext.TufLevelId != tufLevelId.Value)
    {
      StopActiveReplay("different_tuf_level_opened:" + tufLevelId.Value);
    }
  }

  public static void RequestReplayPitchApplyAfterLevelLoad()
  {
    if (_activeContext?.Meta?.levelPitchPercent == null) return;

    _pendingReplayPitchApplyFrame = Time.frameCount + 1;
  }

  public static void TickReplayPitchEditorApply()
  {
    if (_pendingReplayPitchApplyFrame < 0) return;
    if (Time.frameCount < _pendingReplayPitchApplyFrame) return;

    if (_activeContext?.Meta == null)
    {
      _pendingReplayPitchApplyFrame = -1;
      return;
    }

    if (!IsReplayLevelStillCurrent())
    {
      _pendingReplayPitchApplyFrame = -1;
      return;
    }

    ReplayMetadata meta = _activeContext.Meta;
    int? pitchPercent = meta.levelPitchPercent;
    if (!pitchPercent.HasValue)
    {
      _pendingReplayPitchApplyFrame = -1;
      return;
    }

    ReplayPitchApplyResult result = ReplayPitchService.ApplyToEditorLevelData(pitchPercent.Value);
    if (result == ReplayPitchApplyResult.NotReady) return;

    _pendingReplayPitchApplyFrame = -1;

    if (result == ReplayPitchApplyResult.Applied)
    {
      Main.Instance?.Log(
        "[ReplaySessionService] Applied replay pitch as editor change. " +
        ", levelPitchPercent=" + pitchPercent.Value +
        ", effectivePitch=" + (meta.effectivePitch?.ToString("F6") ?? "null")
      );
    }
  }

  public static void StopActiveReplay(string reason)
  {
    ClearActiveContext();
    Main.Instance?.Log("[ReplaySessionService] Active replay context cleared. reason=" + reason);
  }

  public static void ClearActiveContext()
  {
    bool hadActiveContext = _activeContext != null;

    _activeContext?.NativeInputPlayer?.ReleaseAll();
    _activeContext = null;
    _pendingReplayPitchApplyFrame = -1;
    _suppressReplayMarkFail = false;

    if (hadActiveContext && ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(false);
    }
  }

  public static void OnStateChanged(States newState)
  {
    if (_activeContext == null) return;

    if (!IsReplayLevelStillCurrent())
    {
      return;
    }

    switch (newState)
    {
      case States.Countdown:
        ResetReplayRun("state_countdown");
        break;

      case States.PlayerControl:
        if (!_activeContext.RunStarted)
          ResetReplayRun("state_player_control_without_countdown");
        break;

      case States.Start:
        if (_activeContext.RunStarted)
          PrepareReplayRunRestart("state_start_after_replay_run");
        break;
    }
  }

  private static bool IsReplayLevelStillCurrent()
  {
    if (_activeContext == null) return false;

    if (!TufHelperGateway.IsFromTUFHelper())
    {
      StopActiveReplay("not_from_tuf_helper");
      return false;
    }

    int? levelId = TufHelperGateway.GetLevelID();
    if (!levelId.HasValue)
    {
      StopActiveReplay("tuf_level_id_missing");
      return false;
    }

    if (_activeContext.TufLevelId != levelId.Value)
    {
      StopActiveReplay("different_tuf_level_current:" + levelId.Value);
      return false;
    }

    return true;
  }

  private static void PrepareReplayRunRestart(string reason)
  {
    if (_activeContext == null) return;

    ReplayRunController.MarkRestartPrepared(_activeContext);
    _suppressReplayMarkFail = false;

    if (ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(false);
    }

    Main.Instance?.Log("[ReplaySessionService] Replay run restart prepared. reason=" + reason);
  }

  private static void ResetReplayRun(string reason)
  {
    if (_activeContext == null) return;

    bool hasReplayTime = TryComputeReplayTimeUs(out long nowUs, out string timeReason);
    int restoredNativeKeys = 0;

    if (hasReplayTime)
    {
      restoredNativeKeys = _activeContext.NativeInputPlayer?.ResetTo(nowUs) ?? 0;
    }
    else
    {
      _activeContext.NativeInputPlayer?.Reset();
    }

    bool skipPassedAngles = TryGetControllerState(out States state) && state == States.PlayerControl;
    int skippedHitContexts = _activeContext.HitContextPlayer?.ResetTo(ADOBase.controller, skipPassedAngles) ?? 0;

    _activeContext.RunStarted = true;
    _suppressReplayMarkFail = UsesHitContextPlayback;

    if (ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(ShouldUseReplayNoFail());
    }

    _lastNativeGateLogKey = null;
    _lastNativeGateLogFrame = -100000;
    _lastNativeTickLogFrame = -100000;
    _lastHitContextTickLogFrame = -100000;

    Main.Instance?.Log(
      "[ReplaySessionService] Replay run reset. reason=" + reason +
      ", nowUs=" + (hasReplayTime ? nowUs.ToString() : "unavailable:" + timeReason) +
      ", restoredNativeKeys=" + restoredNativeKeys +
      ", skippedHitContexts=" + skippedHitContexts +
      ", skipPassedAngles=" + skipPassedAngles +
      ", noFail=" + ShouldUseReplayNoFail() +
      ", native=" + SchedulerSnapshot(_activeContext.NativeInputScheduler) +
      ", hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer) +
      ", " + DescribeControllerState()
    );
  }

  public static bool ShouldBlockOriginalHit()
  {
    return UsesHitContextPlayback && _activeContext.RunStarted;
  }

  private static bool ShouldUseReplayNoFail()
  {
    return ReplayFailPolicy.ShouldUseReplayNoFail(_activeContext, UsesHitContextPlayback);
  }

  public static bool ShouldSuppressReplayMarkFail()
  {
    return UsesHitContextPlayback && _suppressReplayMarkFail;
  }

  public static void AllowReplayMarkFailOnce()
  {
    _suppressReplayMarkFail = false;
  }

  public static void SuppressReplayMarkFail()
  {
    if (UsesHitContextPlayback)
    {
      _suppressReplayMarkFail = true;
    }
  }

  public static bool ShouldBlockFreeroam(scrController controller)
  {
    return _activeContext?.HitContextPlayer?.ShouldBlockFreeroam(controller) ?? false;
  }

  public static void TickHitContextPlayback(scrController controller)
  {
    if (!UsesHitContextPlayback || controller == null) return;
    if (!TryGetControllerState(out States state) || state != States.PlayerControl) return;

    int before = _activeContext.HitContextPlayer.NextIndex;
    HitContextTickResult result = _activeContext.HitContextPlayer.Tick(controller);
    int after = _activeContext.HitContextPlayer.NextIndex;

    if (result.HasError)
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Playback error. error=" + result.ErrorMessage +
        ", before=" + before +
        ", after=" + after +
        ", hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer) +
        ", " + DescribeControllerState()
      );
      StopActiveReplay("hit_context_error");
      return;
    }

    if (result.PlayedAny)
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Played hits=" + result.PlayedCount +
        ", ignoredFalseResults=" + result.IgnoredFalseResultCount +
        ", before=" + before +
        ", after=" + after +
        ", hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer) +
        ", " + DescribeControllerState()
      );
    }
    else
    {
      LogHitContextIdle(before, after);
    }

    if (_activeContext?.HitContextPlayer != null && _activeContext.HitContextPlayer.Finished)
    {
      Main.Instance?.Log("[Replay/HitContext] Finished. hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer));
    }
  }

  public static int TickNativeVisual(long nowUs)
  {
    int before = _activeContext?.NativeInputScheduler?.NextIndex ?? -1;
    int emitted = _activeContext?.NativeInputPlayer?.Tick(nowUs) ?? 0;
    int after = _activeContext?.NativeInputScheduler?.NextIndex ?? -1;

    if (emitted > 0)
    {
      Main.Instance.Log(
        "[Replay/InputDebug] Native emitted=" + emitted +
        ", nowUs=" + nowUs +
        ", scheduler=" + SchedulerSnapshot(_activeContext?.NativeInputScheduler)
      );
    }
    else
    {
      LogTickIdle(
        "Native",
        nowUs,
        _activeContext?.NativeInputScheduler,
        before,
        after,
        ref _lastNativeTickLogFrame
      );
    }

    return emitted;
  }

  private static bool TryComputeReplayTimeUs(out long nowUs, out string reason)
  {
    nowUs = 0L;
    reason = null;

    return ReplayClock.TryComputeReplayTimeUs(_activeContext, out nowUs, out reason);
  }

  public static bool TryGetNativeReplayTimeUs(out long nowUs)
  {
    nowUs = 0L;

    if (!UnityEngine.Application.isFocused)
    {
      _activeContext?.NativeInputPlayer?.ReleaseAll();
      LogGateBlocked("Native", "application_not_focused", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (!TryGetControllerState(out States state))
    {
      LogGateBlocked("Native", "controller_missing", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (state != States.Countdown &&
        state != States.PlayerControl &&
        state != States.Won)
    {
      LogGateBlocked("Native", "state_not_replay_window", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (!TryComputeReplayTimeUs(out nowUs, out string reason))
    {
      LogGateBlocked("Native", reason, ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    return true;
  }

  private static void LogGateBlocked(string channel, string reason, ref string lastKey, ref int lastFrame)
  {
    int frame = Time.frameCount;
    string key = reason + "|" + DescribeControllerState();
    if (key == lastKey && frame - lastFrame < 120) return;

    lastKey = key;
    lastFrame = frame;

    Main.Instance?.Log(
      "[Replay/InputDebug] " + channel + " gate blocked. reason=" + reason +
      ", frame=" + frame +
      ", " + DescribeControllerState() +
      ", conductor=" + DescribeConductor() +
      ", scheduler=" + SchedulerSnapshot(_activeContext?.NativeInputScheduler)
    );
  }

  private static void LogTickIdle(
    string channel,
    long nowUs,
    ReplayInputScheduler scheduler,
    int before,
    int after,
    ref int lastFrame
  )
  {
    int frame = Time.frameCount;
    bool advanced = before != after;
    if (!advanced && frame - lastFrame < 120) return;

    lastFrame = frame;
    Main.Instance?.Log(
      "[Replay/InputDebug] " + channel + " tick. emitted=0" +
      ", nowUs=" + nowUs +
      ", before=" + before +
      ", after=" + after +
      ", scheduler=" + SchedulerSnapshot(scheduler) +
      ", " + DescribeControllerState()
    );
  }

  private static void LogHitContextIdle(int before, int after)
  {
    int frame = Time.frameCount;
    bool advanced = before != after;
    if (!advanced && frame - _lastHitContextTickLogFrame < 120) return;

    _lastHitContextTickLogFrame = frame;
    Main.Instance?.Log(
      "[Replay/HitContext] Tick. played=0" +
      ", before=" + before +
      ", after=" + after +
      ", hitContext=" + HitContextSnapshot(_activeContext?.HitContextPlayer) +
      ", " + DescribeControllerState()
    );
  }

  private static string SchedulerSnapshot(ReplayInputScheduler scheduler)
  {
    if (scheduler == null) return "null";

    RecordedInput? next = scheduler.PeekNext();
    string nextText = next.HasValue
      ? next.Value.TimeUs + "/" + next.Value.Key + "/" + next.Value.Flags
      : "none";

    return scheduler.NextIndex + "/" + scheduler.Count + ", next=" + nextText;
  }

  private static string HitContextSnapshot(ReplayHitContextPlayer player)
  {
    if (player == null) return "null";

    ReplayHitContext? next = player.PeekNext();
    string nextText = next.HasValue
      ? next.Value.CurrentFloorID + "/" + next.Value.CurrAngle.ToString("F6") + "/freeRoam=" + next.Value.CurFreeRoamSection
      : "none";

    return player.NextIndex + "/" + player.Count + ", next=" + nextText;
  }

  private static string DescribeControllerState()
  {
    if (ADOBase.controller == null) return "controller=null";

    string machineState;
    try
    {
      machineState = ADOBase.controller.stateMachine?.GetState()?.ToString() ?? "null";
    }
    catch (Exception ex)
    {
      machineState = "error:" + ex.GetType().Name;
    }

    return
      "state=" + ADOBase.controller.state +
      ", currentState=" + ADOBase.controller.currentState +
      ", machineState=" + machineState +
      ", paused=" + ADOBase.controller.paused;
  }

  private static string DescribeConductor()
  {
    if (ADOBase.conductor == null) return "null";

    string start = _activeContext?.Meta?.gameplayStartSongPosition?.ToString("F6") ?? "null";
    return "songposition_minusi=" + ADOBase.conductor.songposition_minusi.ToString("F6") + ", start=" + start;
  }

  private static bool TryGetControllerState(out States state)
  {
    state = default;

    if (ADOBase.controller == null) return false;

    try
    {
      object machineState = ADOBase.controller.stateMachine?.GetState();
      if (machineState is States states)
      {
        state = states;
        return true;
      }
    }
    catch
    {
    }

    state = ADOBase.controller.state;
    return true;
  }
}
