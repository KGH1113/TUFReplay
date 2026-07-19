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
  private static int _lastHitContextTickLogFrame = -100000;
  private static bool _nativeFocusBlocked;
  private static string _nativeFocusBlockReason;
  private static long _nativeFocusSkippedEvents;
  private static int _pendingReplayPitchApplyFrame = -1;
  private static bool _suppressReplayMarkFail;

  public static bool HasActiveContext => _activeContext != null;
  public static bool UsesHitContextPlayback => _activeContext?.HitContextPlayer?.Count > 0;
  public static string ActiveRunId => _activeContext?.RunId;
  public static bool NativeInputFinished => _activeContext?.NativeInputPlayer?.Finished == true;
  public static bool HitContextFinished =>
    _activeContext?.HitContextPlayer == null || _activeContext.HitContextPlayer.Finished;
  public static string ActiveResult => _activeContext?.Result;
  public static long ActiveTerminalTimeUs => _activeContext?.TerminalTimeUs ?? 0L;

  public static bool IsActiveReplayLevel()
  {
    return _activeContext != null && IsActiveReplayHashCurrent();
  }

  public static void ClearActiveContextIfLevelChanged()
  {
    if (_activeContext == null)
      return;
    if (!IsActiveReplayHashCurrent())
      StopActiveReplay("different_level_opened");
  }

  public static void InstallActiveContext(ActiveReplayContext context)
  {
    if (context == null)
      throw new ArgumentNullException(nameof(context));

    ClearActiveContext();
    _activeContext = context;
    _activeContext.Phase = ReplayPlaybackPhase.Prepared;
    LogLifecycleTransition(ReplayPlaybackPhase.Stopped, ReplayPlaybackPhase.Prepared, "context_installed");
    _lastNativeGateLogKey = null;
    _lastNativeGateLogFrame = -100000;
    _lastHitContextTickLogFrame = -100000;
    ResetNativeFocusTracking();
    _suppressReplayMarkFail = false;
  }

  public static void RequestReplayPitchApplyAfterLevelLoad()
  {
    if (_activeContext?.Meta?.levelPitchPercent == null)
      return;

    _pendingReplayPitchApplyFrame = Time.frameCount + 1;
  }

  public static void TickReplayPitchEditorApply()
  {
    if (_pendingReplayPitchApplyFrame < 0)
      return;
    if (Time.frameCount < _pendingReplayPitchApplyFrame)
      return;

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
    if (!meta.levelPitchPercent.HasValue)
    {
      _pendingReplayPitchApplyFrame = -1;
      return;
    }

    if (!ReplayPitchService.GetEditorPitch().HasValue)
      return;
    ApplyReplayPitchNow();
    _pendingReplayPitchApplyFrame = -1;
  }

  public static void StopActiveReplay(string reason)
  {
    ClearActiveContext();
    Main.Instance?.Log("[ReplaySessionService] Active replay context cleared. reason=" + reason);
  }

  public static void ClearActiveContext()
  {
    ActiveReplayContext context = _activeContext;
    bool hadActiveContext = context != null;

    if (context != null)
    {
      LogNativeSummary(context, "stopped");
      LogLifecycleTransition(context.Phase, ReplayPlaybackPhase.Stopped, "context_cleared");
      context.Phase = ReplayPlaybackPhase.Stopped;
      context.NativeInputPlayer?.Dispose();
      context.MicrophonePlayer?.Dispose();
    }
    RestoreReplayPitch();
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
    if (_activeContext == null)
      return;

    if (!IsReplayLevelStillCurrent())
    {
      return;
    }

    if (
      newState == States.Won
      && (_activeContext.Phase == ReplayPlaybackPhase.Armed || _activeContext.Phase == ReplayPlaybackPhase.Running)
    )
    {
      ReplayClock.EnterWon(_activeContext);
      TransitionTo(ReplayPlaybackPhase.Won, "state_won");
    }

    ReplayPlaybackCoordinator.OnGameStateChanged(newState);

    switch (newState)
    {
      case States.Countdown:
        if (_activeContext.Phase == ReplayPlaybackPhase.Prepared)
          ResetReplayRun("state_countdown", ReplayPlaybackPhase.Armed);
        break;

      case States.PlayerControl:
        if (_activeContext.Phase == ReplayPlaybackPhase.Armed)
          TransitionTo(ReplayPlaybackPhase.Running, "state_player_control");
        break;

      case States.Won:
      case States.Fail:
      case States.Fail2:
        _activeContext.MicrophonePlayer?.Stop();
        break;

      case States.Start:
        if (_activeContext.RunStarted)
          PrepareReplayRunRestart("state_start_after_replay_run");
        break;
    }
  }

  private static bool IsReplayLevelStillCurrent()
  {
    if (_activeContext == null)
      return false;

    if (!IsActiveReplayHashCurrent())
    {
      StopActiveReplay("different_level_current");
      ReplayPlaybackCoordinator.Fail("different_level_current", "The open level changed during replay.");
      return false;
    }

    return true;
  }

  private static bool IsActiveReplayHashCurrent()
  {
    return _activeContext != null
      && GameplayChartHash.IsSupported(_activeContext.GameplayHashVersion, _activeContext.GameplayHash)
      && GameplayChartHash.TryComputeCurrent(out byte[] currentHash, out _)
      && GameplayChartHash.Equals(_activeContext.GameplayHash, currentHash);
  }

  private static void PrepareReplayRunRestart(string reason)
  {
    if (_activeContext == null)
      return;

    ReplayPlaybackPhase previous = _activeContext.Phase;
    ReplayRunController.MarkRestartPrepared(_activeContext);
    _activeContext.MicrophonePlayer?.Stop();
    LogLifecycleTransition(previous, ReplayPlaybackPhase.Prepared, reason);
    _suppressReplayMarkFail = false;

    if (ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(false);
    }

    Main.Instance?.Log("[ReplaySessionService] Replay run restart prepared. reason=" + reason);
  }

  private static void ResetReplayRun(string reason, ReplayPlaybackPhase phase)
  {
    if (_activeContext == null)
      return;

    bool hasReplayTime = TryComputeReplayTimeUs(out long nowUs, out string timeReason);
    int restoredNativeKeys = 0;

    if (hasReplayTime)
    {
      restoredNativeKeys = _activeContext.NativeInputPlayer?.ResetTo(nowUs, CurrentTimelineRate()) ?? 0;
      _activeContext.MicrophonePlayer?.ResetTo(nowUs, CurrentGameplayRate(), CurrentWonTimeUs());
    }
    else
    {
      _activeContext.NativeInputPlayer?.Reset();
      _activeContext.MicrophonePlayer?.ResetTo(0L, CurrentGameplayRate(), CurrentWonTimeUs());
    }

    bool skipPassedAngles = TryGetControllerState(out States state) && state == States.PlayerControl;
    int skippedHitContexts = _activeContext.HitContextPlayer?.ResetTo(ADOBase.controller, skipPassedAngles) ?? 0;
    ResetReplayHeldInputState();

    _activeContext.RunStarted = true;
    TransitionTo(phase, reason);
    _suppressReplayMarkFail = true;

    if (ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(ShouldUseReplayNoFail());
    }

    _lastNativeGateLogKey = null;
    _lastNativeGateLogFrame = -100000;
    _lastHitContextTickLogFrame = -100000;
    ResetNativeFocusTracking();

    Main.Instance?.Log(
      "[ReplaySessionService] Replay run reset. reason="
        + reason
        + ", nowUs="
        + (hasReplayTime ? nowUs.ToString() : "unavailable:" + timeReason)
        + ", restoredNativeKeys="
        + restoredNativeKeys
        + ", skippedHitContexts="
        + skippedHitContexts
        + ", skipPassedAngles="
        + skipPassedAngles
        + ", noFail="
        + ShouldUseReplayNoFail()
        + ", native="
        + SchedulerSnapshot(_activeContext.NativeInputScheduler)
        + ", hitContext="
        + HitContextSnapshot(_activeContext.HitContextPlayer)
        + ", "
        + DescribeControllerState()
    );
  }

  public static bool ShouldBlockOriginalHit()
  {
    return ShouldSuppressGameplayInput();
  }

  public static bool ShouldSuppressGameplayInput()
  {
    if (_activeContext?.RunStarted != true)
      return false;

    ReplayPlaybackPhase phase = _activeContext.Phase;
    return phase == ReplayPlaybackPhase.Armed
      || phase == ReplayPlaybackPhase.Running
      || phase == ReplayPlaybackPhase.Won;
  }

  private static bool ShouldUseReplayNoFail()
  {
    return ReplayFailPolicy.ShouldUseReplayNoFail(_activeContext);
  }

  public static bool ShouldSuppressReplayMarkFail()
  {
    return _activeContext?.RunStarted == true && _suppressReplayMarkFail;
  }

  public static void AllowReplayMarkFailOnce()
  {
    _suppressReplayMarkFail = false;
  }

  public static void SuppressReplayMarkFail()
  {
    if (_activeContext?.RunStarted == true)
      _suppressReplayMarkFail = true;
  }

  public static bool ShouldBlockFreeroam(scrController controller)
  {
    return _activeContext?.HitContextPlayer?.ShouldBlockFreeroam(controller) ?? false;
  }

  public static void TickHitContextPlayback(scrController controller)
  {
    if (!UsesHitContextPlayback || controller == null)
      return;
    if (controller.paused)
      return;
    if (!TryGetControllerState(out States state) || state != States.PlayerControl)
      return;

    int before = _activeContext.HitContextPlayer.NextIndex;
    HitContextTickResult result = _activeContext.HitContextPlayer.Tick(controller);
    int after = _activeContext.HitContextPlayer.NextIndex;

    if (result.HasError)
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Playback error. error="
          + result.ErrorMessage
          + ", before="
          + before
          + ", after="
          + after
          + ", hitContext="
          + HitContextSnapshot(_activeContext.HitContextPlayer)
          + ", "
          + DescribeControllerState()
      );
      StopActiveReplay("hit_context_error");
      return;
    }

    if (result.PlayedAny)
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Played hits="
          + result.PlayedCount
          + ", ignoredFalseResults="
          + result.IgnoredFalseResultCount
          + ", before="
          + before
          + ", after="
          + after
          + ", hitContext="
          + HitContextSnapshot(_activeContext.HitContextPlayer)
          + ", "
          + DescribeControllerState()
      );
    }
    else
    {
      LogHitContextIdle(before, after);
    }

    if (_activeContext?.HitContextPlayer != null && _activeContext.HitContextPlayer.Finished)
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Finished. hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer)
      );
    }
  }

  public static int TickNativeVisual(long nowUs)
  {
    int emitted = _activeContext?.NativeInputPlayer?.Tick(nowUs, CurrentTimelineRate()) ?? 0;

    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
    return emitted;
  }

  public static void TickMicrophonePlayback(long nowUs)
  {
    IReplayMicrophonePlayer player = _activeContext?.MicrophonePlayer;
    if (player == null)
      return;
    if (
      !TryGetControllerState(out States state)
      || (
        state != States.Countdown && state != States.Checkpoint && state != States.PlayerControl && state != States.Won
      )
    )
      return;

    player.Tick(nowUs, CurrentGameplayRate(), CurrentWonTimeUs(), ADOBase.controller.paused);
  }

  public static bool TryGetPlaybackSnapshot(out long replayTimeUs, out double timelineRate)
  {
    timelineRate = CurrentTimelineRate();
    return TryComputeReplayTimeUs(out replayTimeUs, out _);
  }

  public static void UpdateActiveMicrophoneSettings(int offsetMs, int volumeDb)
  {
    IReplayMicrophonePlayer player = _activeContext?.MicrophonePlayer;
    if (player == null || !TryComputeReplayTimeUs(out long replayTimeUs, out _))
      return;
    player.UpdateUserSettings(offsetMs, volumeDb, replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
  }

  public static void ApplyReplayPitchNow()
  {
    if (_activeContext?.Meta?.levelPitchPercent == null)
      return;
    if (_activeContext.ReplayPitchApplied)
      return;

    int? originalPitch = ReplayPitchService.GetEditorPitch();
    if (!originalPitch.HasValue)
      return;

    ReplayPitchApplyResult result = ReplayPitchService.ApplyToEditorLevelData(
      _activeContext.Meta.levelPitchPercent.Value
    );
    if (result != ReplayPitchApplyResult.Applied)
      return;

    _activeContext.OriginalLevelPitchPercent = originalPitch;
    _activeContext.ReplayPitchApplied = true;
  }

  private static void RestoreReplayPitch()
  {
    if (_activeContext?.ReplayPitchApplied != true || !_activeContext.OriginalLevelPitchPercent.HasValue)
      return;

    ReplayPitchService.ApplyToEditorLevelData(_activeContext.OriginalLevelPitchPercent.Value);
    _activeContext.ReplayPitchApplied = false;
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

    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
    {
      LogGateBlocked("Native", "native_player_missing", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    ReplayPlaybackPhase phase = _activeContext.Phase;
    if (phase != ReplayPlaybackPhase.Armed && phase != ReplayPlaybackPhase.Running && phase != ReplayPlaybackPhase.Won)
    {
      LogGateBlocked("Native", "phase_" + phase, ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    bool focusReady = player.CanEmit(out string focusReason);
    if (!focusReady)
      player.ReleaseAll();

    if (!TryGetControllerState(out States state))
    {
      LogGateBlocked("Native", "controller_missing", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (state != States.Countdown && state != States.PlayerControl && state != States.Won)
    {
      LogGateBlocked("Native", "state_not_replay_window", ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (!TryComputeReplayTimeUs(out nowUs, out string reason))
    {
      LogGateBlocked("Native", reason, ref _lastNativeGateLogKey, ref _lastNativeGateLogFrame);
      return false;
    }

    if (!focusReady)
    {
      int skipped = player.SkipTo(nowUs);
      LogNativeFocusBlocked(focusReason, skipped, nowUs, player);
      ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
      return false;
    }

    LogNativeFocusResumed(nowUs, player);

    return true;
  }

  private static void LogNativeFocusBlocked(string reason, int skipped, long nowUs, ReplayNativeInputPlayer player)
  {
    _nativeFocusSkippedEvents += skipped;
    if (_nativeFocusBlocked)
      return;

    _nativeFocusBlocked = true;
    _nativeFocusBlockReason = reason;
    Main.Instance?.Log(
      "[Replay/InputDebug] Native focus blocked. reason="
        + reason
        + ", nowUs="
        + nowUs
        + ", skipped="
        + skipped
        + ", "
        + player.DescribeFocus()
        + ", scheduler="
        + SchedulerSnapshot(_activeContext?.NativeInputScheduler)
    );
  }

  private static void LogNativeFocusResumed(long nowUs, ReplayNativeInputPlayer player)
  {
    if (!_nativeFocusBlocked)
      return;

    Main.Instance?.Log(
      "[Replay/InputDebug] Native focus resumed. previousReason="
        + _nativeFocusBlockReason
        + ", nowUs="
        + nowUs
        + ", skipped="
        + _nativeFocusSkippedEvents
        + ", "
        + player.DescribeFocus()
        + ", scheduler="
        + SchedulerSnapshot(_activeContext?.NativeInputScheduler)
    );
    ResetNativeFocusTracking();
  }

  private static void ResetNativeFocusTracking()
  {
    _nativeFocusBlocked = false;
    _nativeFocusBlockReason = null;
    _nativeFocusSkippedEvents = 0L;
  }

  private static double CurrentTimelineRate()
  {
    if (_activeContext?.Phase == ReplayPlaybackPhase.Won)
      return 1d;

    float? effectivePitch = _activeContext?.Meta?.effectivePitch;
    return effectivePitch.HasValue && effectivePitch.Value > 0f ? effectivePitch.Value : 1d;
  }

  private static double CurrentGameplayRate()
  {
    float? effectivePitch = _activeContext?.Meta?.effectivePitch;
    return effectivePitch.HasValue && effectivePitch.Value > 0f ? effectivePitch.Value : 1d;
  }

  private static long? CurrentWonTimeUs() => _activeContext?.Meta?.wonTimeUs;

  private static void ResetReplayHeldInputState()
  {
    scrPlayer player = ADOBase.controller?.playerOne;
    if (player == null)
      return;

    player.keyTimes?.Clear();
    player.holdKeys?.Clear();
  }

  private static void TransitionTo(ReplayPlaybackPhase phase, string reason)
  {
    if (_activeContext == null || _activeContext.Phase == phase)
      return;

    ReplayPlaybackPhase previous = _activeContext.Phase;
    _activeContext.Phase = phase;
    LogLifecycleTransition(previous, phase, reason);
  }

  private static void LogLifecycleTransition(ReplayPlaybackPhase previous, ReplayPlaybackPhase phase, string reason)
  {
    Main.Instance?.Log(
      "[Replay/Lifecycle] " + previous + " -> " + phase + ". reason=" + reason + ", runId=" + _activeContext.RunId
    );
  }

  private static void LogNativeSummary(ActiveReplayContext context, string reason)
  {
    ReplayNativeInputStats? stats = context?.NativeInputPlayer?.Stats;
    if (!stats.HasValue)
      return;

    ReplayNativeInputStats value = stats.Value;
    Main.Instance?.Log(
      "[Replay/Input] Pump summary. reason="
        + reason
        + ", emitted="
        + value.Emitted
        + ", stateSeeks="
        + value.StateSeeks
        + ", emissionFailures="
        + value.EmissionFailures
        + ", maxLatenessUs="
        + value.MaxLatenessUs
        + ", index="
        + value.NextIndex
        + "/"
        + value.Count
    );
  }

  private static void LogGateBlocked(string channel, string reason, ref string lastKey, ref int lastFrame)
  {
    int frame = Time.frameCount;
    string key = reason + "|" + DescribeControllerState();
    if (key == lastKey && frame - lastFrame < 120)
      return;

    lastKey = key;
    lastFrame = frame;

    Main.Instance?.Log(
      "[Replay/InputDebug] "
        + channel
        + " gate blocked. reason="
        + reason
        + ", frame="
        + frame
        + ", "
        + DescribeControllerState()
        + ", conductor="
        + DescribeConductor()
        + ", scheduler="
        + SchedulerSnapshot(_activeContext?.NativeInputScheduler)
    );
  }

  private static void LogHitContextIdle(int before, int after)
  {
    int frame = Time.frameCount;
    bool advanced = before != after;
    if (!advanced && frame - _lastHitContextTickLogFrame < 120)
      return;

    _lastHitContextTickLogFrame = frame;
    Main.Instance?.Log(
      "[Replay/HitContext] Tick. played=0"
        + ", before="
        + before
        + ", after="
        + after
        + ", hitContext="
        + HitContextSnapshot(_activeContext?.HitContextPlayer)
        + ", "
        + DescribeControllerState()
    );
  }

  private static string SchedulerSnapshot(ReplayInputScheduler scheduler)
  {
    if (scheduler == null)
      return "null";

    RecordedInput? next = scheduler.PeekNext();
    string nextText = next.HasValue ? next.Value.TimeUs + "/" + next.Value.Key + "/" + next.Value.Flags : "none";

    return scheduler.NextIndex + "/" + scheduler.Count + ", next=" + nextText;
  }

  private static string HitContextSnapshot(ReplayHitContextPlayer player)
  {
    if (player == null)
      return "null";

    ReplayHitContext? next = player.PeekNext();
    string nextText = next.HasValue
      ? next.Value.CurrentFloorID
        + "/"
        + next.Value.CurrAngle.ToString("F6")
        + "/freeRoam="
        + next.Value.CurFreeRoamSection
      : "none";

    return player.NextIndex + "/" + player.Count + ", next=" + nextText;
  }

  private static string DescribeControllerState()
  {
    if (ADOBase.controller == null)
      return "controller=null";

    string machineState;
    try
    {
      machineState = ADOBase.controller.stateMachine?.GetState()?.ToString() ?? "null";
    }
    catch (Exception ex)
    {
      machineState = "error:" + ex.GetType().Name;
    }

    return "state="
      + ADOBase.controller.state
      + ", currentState="
      + ADOBase.controller.currentState
      + ", machineState="
      + machineState
      + ", paused="
      + ADOBase.controller.paused;
  }

  private static string DescribeConductor()
  {
    if (ADOBase.conductor == null)
      return "null";

    string start = _activeContext?.Meta?.gameplayStartSongPosition?.ToString("F6") ?? "null";
    return "songposition_minusi=" + ADOBase.conductor.songposition_minusi.ToString("F6") + ", start=" + start;
  }

  private static bool TryGetControllerState(out States state)
  {
    state = default;

    if (ADOBase.controller == null)
      return false;

    try
    {
      object machineState = ADOBase.controller.stateMachine?.GetState();
      if (machineState is States states)
      {
        state = states;
        return true;
      }
    }
    catch { }

    state = ADOBase.controller.state;
    return true;
  }
}
