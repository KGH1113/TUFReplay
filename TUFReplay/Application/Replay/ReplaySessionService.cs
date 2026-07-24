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
    if (_activeContext == null)
      return;

    switch (newState)
    {
      case States.Countdown:
        if (_activeContext.Phase == ReplayPlaybackPhase.Prepared)
          ResetReplayRun("state_countdown", ReplayPlaybackPhase.Armed);
        break;

      case States.PlayerControl:
        if (ReplayRunController.ShouldInitializeFromPlayerControl(_activeContext))
          EnsurePlayerControlRunStarted();
        else if (_activeContext.Phase == ReplayPlaybackPhase.Armed)
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

    bool hasReplayTime = TryComputeReplayTimeUs(out long nowUs, out _);

    if (hasReplayTime)
    {
      _activeContext.NativeInputPlayer?.ResetTo(nowUs, CurrentTimelineRate());
      _activeContext.MicrophonePlayer?.ResetTo(nowUs, CurrentGameplayRate(), CurrentWonTimeUs());
    }
    else
    {
      _activeContext.NativeInputPlayer?.Reset();
      _activeContext.MicrophonePlayer?.ResetTo(0L, CurrentGameplayRate(), CurrentWonTimeUs());
    }

    bool skipPassedAngles = TryGetControllerState(out States state) && state == States.PlayerControl;
    _activeContext.HitContextPlayer?.ResetTo(ADOBase.controller, skipPassedAngles);
    ResetReplayHeldInputState();

    _activeContext.RunStarted = true;
    TransitionTo(phase, reason);
    _suppressReplayMarkFail = true;

    if (ADOBase.controller != null)
    {
      ReplayFailPolicy.ApplyReplayNoFail(ShouldUseReplayNoFail());
    }
  }

  public static bool ShouldBlockOriginalHit()
  {
    return ShouldSuppressGameplayInput();
  }

  public static bool ShouldSuppressGameplayInput()
  {
    if (_activeContext == null)
      return false;

    if (!_activeContext.RunStarted)
    {
      return ReplayRunController.ShouldInitializeFromPlayerControl(_activeContext)
        && TryGetControllerState(out States state)
        && state == States.PlayerControl;
    }

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
    if (!EnsurePlayerControlRunStarted())
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

    if (!EnsurePlayerControlRunStarted())
      return false;

    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
      return false;

    ReplayPlaybackPhase phase = _activeContext.Phase;
    if (phase != ReplayPlaybackPhase.Armed && phase != ReplayPlaybackPhase.Running && phase != ReplayPlaybackPhase.Won)
      return false;

    bool focusReady = player.CanEmit(out _);
    if (!focusReady)
      player.ReleaseAll();

    if (!TryGetControllerState(out States state))
      return false;

    if (state != States.Countdown && state != States.PlayerControl && state != States.Won)
      return false;

    if (!TryComputeReplayTimeUs(out nowUs, out _))
      return false;

    if (!focusReady)
    {
      player.SkipTo(nowUs);
      ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
      return false;
    }

    return true;
  }

  private static bool EnsurePlayerControlRunStarted()
  {
    if (_activeContext == null)
      return false;
    if (_activeContext.RunStarted)
      return true;
    if (!ReplayRunController.ShouldInitializeFromPlayerControl(_activeContext))
      return false;
    if (!TryGetControllerState(out States state) || state != States.PlayerControl)
      return false;
    if (!TryComputeReplayTimeUs(out _, out _))
      return false;

    ResetReplayRun("player_control_without_countdown", ReplayPlaybackPhase.Running);
    return _activeContext?.RunStarted == true;
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
