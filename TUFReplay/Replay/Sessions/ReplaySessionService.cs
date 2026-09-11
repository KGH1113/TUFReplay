using System;
using System.Collections.Generic;
using TUFReplay.Activity.Charts;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Transport;
using UnityEngine;

namespace TUFReplay.Replay.Sessions;

public static partial class ReplaySessionService
{
  private static ActiveReplayContext _activeContext;
  private static int _pendingReplayPitchApplyFrame = -1;
  private static bool _suppressReplayMarkFail;
  private static bool _playbackPauseSuspended;
  private static string _lastStartupBlockReason;
  private static bool _timelineScrubActive;
  private static bool _timelineScrubWasPaused;
  private static long _timelineScrubOriginTimeUs;
  private static bool _timelineRestartPending;
  private static bool _timelineRestartPauseAtPlayerControl;
  private static bool _timelinePauseOwnsEditorState;
  private static bool _timelineEditorPausedBeforePause;

  public static bool TryGetNativeInputStats(out ReplayNativeInputStats stats)
  {
    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
    {
      stats = default;
      return false;
    }
    stats = player.Stats;
    return true;
  }

  public static bool HasActiveContext => _activeContext != null;
  public static bool UsesHitContextPlayback => _activeContext?.HitContextPlayer?.Count > 0;
  public static string ActiveRunId => _activeContext?.RunId;
  public static bool NativeInputFinished => _activeContext?.NativeInputPlayer?.Finished == true;
  public static bool HitContextFinished =>
    _activeContext?.HitContextPlayer == null || _activeContext.HitContextPlayer.Finished;
  public static string ActiveResult => _activeContext?.Result;
  public static long ActiveTerminalTimeUs => _activeContext?.TerminalTimeUs ?? 0L;
  internal static bool IsTimelineRestartPending => _timelineRestartPending;

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
    _playbackPauseSuspended = false;
    _lastStartupBlockReason = null;
    ResetTimelineTransportState();
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
    if (_timelineScrubActive)
      CancelTimelineScrub();

    ActiveReplayContext context = _activeContext;

    if (context != null)
    {
      LogNativeSummary(context, "stopped");
      LogLifecycleTransition(context.Phase, ReplayPlaybackPhase.Stopped, "context_cleared");
      context.Phase = ReplayPlaybackPhase.Stopped;
      context.NativeInputPlayer?.Dispose();
      context.MicrophonePlayer?.Dispose();
    }
    RestoreReplayNoFail();
    RestoreReplayPitch();
    RestoreReplayJudgmentDifficulty();
    _activeContext = null;
    _pendingReplayPitchApplyFrame = -1;
    _suppressReplayMarkFail = false;
    _playbackPauseSuspended = false;
    _lastStartupBlockReason = null;
    RestoreTimelineEditorPauseState();
    ResetTimelineTransportState();
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
      case States.Checkpoint:
        if (ReplayRunController.ShouldInitializeFromPreRoll(_activeContext))
          ResetReplayRun("state_preroll_" + newState, ReplayPlaybackPhase.Armed);
        if (_timelineRestartPending)
          _activeContext.HitContextPlayer?.ResetToAndRebuildJudgments(ADOBase.controller, skipPassedAngles: false);
        break;

      case States.PlayerControl:
        if (ReplayRunController.ShouldInitializeFromPlayerControl(_activeContext))
          EnsurePlayerControlRunStarted();
        else if (_activeContext.Phase == ReplayPlaybackPhase.Armed)
          TransitionTo(ReplayPlaybackPhase.Running, "state_player_control");
        CompleteTimelineRestart();
        break;

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

  public static void TickStartup()
  {
    if (_activeContext == null || _activeContext.RunStarted)
      return;
    if (!TryGetControllerState(out States state))
      return;

    switch (state)
    {
      case States.Countdown:
      case States.Checkpoint:
        if (ReplayRunController.ShouldInitializeFromPreRoll(_activeContext))
          ResetReplayRun("heartbeat_preroll_" + state, ReplayPlaybackPhase.Armed);
        break;

      case States.PlayerControl:
        EnsurePlayerControlRunStarted();
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
      && GameplayChartHash.TryCompute(
        ADOBase.editor?.levelData ?? ADOBase.customLevel?.levelData,
        out byte[] currentHash,
        out _
      )
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

  private static bool ResetReplayRun(string reason, ReplayPlaybackPhase phase)
  {
    if (_activeContext == null)
      return false;

    if (!TryComputeReplayTimeUs(out long nowUs, out string blockedReason))
    {
      LogStartupBlocked(blockedReason);
      return false;
    }

    ReplayPlaybackSnapshot snapshot = CreatePlaybackSnapshot(nowUs);
    ResetReplayHeldInputState();
    _activeContext.NativeInputPlayer?.ResetTo(snapshot);
    _activeContext.MicrophonePlayer?.ResetTo(snapshot);
    bool skipPassedAngles = TryGetControllerState(out States state) && state == States.PlayerControl;
    _activeContext.HitContextPlayer?.ResetTo(ADOBase.controller, skipPassedAngles);
    _playbackPauseSuspended = false;
    _lastStartupBlockReason = null;

    _activeContext.RunStarted = true;
    TransitionTo(phase, reason);
    _suppressReplayMarkFail = true;

    ApplyReplayNoFailNow();
    return true;
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

    if (
      _activeContext?.HitContextPlayer != null
      && before < _activeContext.HitContextPlayer.Count
      && after >= _activeContext.HitContextPlayer.Count
    )
    {
      Main.Instance?.Log(
        "[Replay/HitContext] Finished. hitContext=" + HitContextSnapshot(_activeContext.HitContextPlayer)
      );
    }
  }

  public static int TickReplayPlayback(long nowUs)
  {
    ReplayPlaybackSnapshot snapshot = CreatePlaybackSnapshot(nowUs);
    int emitted = _activeContext?.NativeInputPlayer?.Tick(snapshot) ?? 0;

    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
    IReplayMicrophonePlayer player = _activeContext?.MicrophonePlayer;
    if (player != null && TryGetControllerState(out States state) && IsReplayTimelinePlaybackState(state))
      player.Tick(snapshot);

    return emitted;
  }

  private static bool TryComputeReplayTimeUs(out long nowUs, out string reason)
  {
    nowUs = 0L;
    reason = null;

    ReplayRuntimeReadiness readiness = GetRuntimeReadiness();
    if (!readiness.Ready)
    {
      reason = readiness.Reason;
      return false;
    }

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

    if (!TryGetControllerState(out States state))
      return false;

    if (!IsReplayTimelinePlaybackState(state))
      return false;

    if (!TryComputeReplayTimeUs(out nowUs, out _))
      return false;

    if (ADOBase.controller.paused)
    {
      SetEditorPausedInPlayMode(true);
      SuspendReplayAt(nowUs);
      return false;
    }

    if (_playbackPauseSuspended)
      ResumeReplayAt(nowUs);

    bool focusReady = player.CanEmit(out _);
    if (!focusReady)
      player.ReleaseAll();

    if (!focusReady)
    {
      player.SkipTo(nowUs);
      ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
      return false;
    }

    return true;
  }

  internal static void SuspendNativeInputForUmmWindow()
  {
    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
      return;

    if (!TryComputeReplayTimeUs(out long nowUs, out _))
    {
      player.ReleaseAll();
      return;
    }

    player.SkipTo(nowUs);
    _playbackPauseSuspended = true;
    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
  }

  internal static void ResumeNativeInputAfterUmmWindow()
  {
    if (_activeContext?.NativeInputPlayer == null || ADOBase.controller == null || ADOBase.controller.paused)
      return;
    if (TryComputeReplayTimeUs(out long nowUs, out _))
      ResumeReplayAt(nowUs);
  }

  internal static void OnNativeInputPauseChanged(bool paused)
  {
    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
      return;
    if (!TryComputeReplayTimeUs(out long nowUs, out _))
    {
      if (paused)
        player.ReleaseAll();
      return;
    }
    if (paused)
      SuspendReplayAt(nowUs);
    else
      ResumeReplayAt(nowUs);
  }

  internal static void OnApplicationFocusChanged(bool focused)
  {
    ReplayNativeInputPlayer player = _activeContext?.NativeInputPlayer;
    if (player == null)
      return;
    if (!focused)
    {
      SuspendNativeInputForUmmWindow();
      return;
    }
    if (ADOBase.controller != null && !ADOBase.controller.paused)
      ResumeNativeInputAfterUmmWindow();
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
    if (!TryComputeReplayTimeUs(out _, out string blockedReason))
    {
      LogStartupBlocked(blockedReason);
      return false;
    }

    return ResetReplayRun("player_control_without_countdown", ReplayPlaybackPhase.Running);
  }

  private static ReplayRuntimeReadiness GetRuntimeReadiness()
  {
    bool hasState = TryGetControllerState(out States state);
    double? startPosition = _activeContext?.Meta?.gameplayStartSongPosition;
    double songPosition = ADOBase.conductor?.songposition_minusi ?? double.NaN;
    bool requiresEditorPlayMode = ADOBase.isLevelEditor;

    return ReplayRuntimeReadinessEvaluator.Evaluate(
      _activeContext != null,
      ADOBase.conductor != null,
      ADOBase.conductor != null && ADOBase.conductor.gameObject.activeInHierarchy,
      ADOBase.controller != null,
      ADOBase.controller != null && ADOBase.controller.gameObject.activeInHierarchy,
      requiresEditorPlayMode,
      !requiresEditorPlayMode || (scnEditor.instance != null && scnEditor.instance.playMode),
      ADOBase.conductor != null
        && ADOBase.conductor.hasSongStarted
        && ADOBase.conductor.song != null
        && ADOBase.conductor.crotchetAtStart > 0d
        && ADOBase.conductor.song.pitch > 0f
        && !float.IsNaN(ADOBase.conductor.song.pitch)
        && !float.IsInfinity(ADOBase.conductor.song.pitch),
      IsFinite(songPosition),
      startPosition.HasValue && IsFinite(startPosition.Value),
      hasState && IsReplayTimelinePlaybackState(state)
    );
  }

  private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

  private static void LogStartupBlocked(string reason)
  {
    if (string.IsNullOrEmpty(reason) || string.Equals(_lastStartupBlockReason, reason, StringComparison.Ordinal))
      return;
    _lastStartupBlockReason = reason;
    Main.Instance?.Log("[Replay/Lifecycle] Replay start blocked. reason=" + reason);
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

  private static ReplayPlaybackSnapshot CreatePlaybackSnapshot(long timelineTimeUs, bool? paused = null) =>
    new ReplayPlaybackSnapshot(
      timelineTimeUs,
      CurrentTimelineRate(),
      CurrentGameplayRate(),
      CurrentWonTimeUs(),
      paused ?? ADOBase.controller?.paused == true
    );

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
        + ", p50LatenessUs="
        + value.P50LatenessUs
        + ", p95LatenessUs="
        + value.P95LatenessUs
        + ", p99LatenessUs="
        + value.P99LatenessUs
        + ", catchUpEvents="
        + value.CatchUpEvents
        + ", partialRetries="
        + value.PartialRetries
        + ", failedEvents="
        + value.FailedEvents
        + ", unsupportedEvents="
        + value.UnsupportedEvents
        + ", waiter="
        + value.Waiter
        + ", waiterFallbacks="
        + value.WaiterFallbacks
        + ", waiterFallbackReason="
        + (value.WaiterFallbackReason ?? "none")
        + ", nativeMetadataEmitted="
        + value.NativeMetadataEmitted
        + ", fallbackEmitted="
        + value.FallbackEmitted
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
