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
  private static bool _playbackPauseSuspended;
  private static bool _timelineScrubActive;
  private static bool _timelineScrubWasPaused;
  private static long _timelineScrubOriginTimeUs;
  private static bool _timelineRestartPending;
  private static bool _timelineRestartPauseAtPlayerControl;
  private static bool _timelinePauseOwnsEditorState;
  private static bool _timelineEditorPausedBeforePause;

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
        _activeContext.GameplayHashVersion,
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
    _playbackPauseSuspended = false;

    _activeContext.RunStarted = true;
    TransitionTo(phase, reason);
    _suppressReplayMarkFail = true;

    ApplyReplayNoFailNow();
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

  public static void ApplyReplayNoFailNow()
  {
    if (_activeContext == null || ADOBase.controller == null)
      return;

    if (!_activeContext.ReplayNoFailApplied)
    {
      _activeContext.OriginalNoFailMode = ADOBase.controller.noFail;
      _activeContext.ReplayNoFailApplied = true;
    }

    ReplayFailPolicy.ApplyReplayNoFail(ShouldUseReplayNoFail());
  }

  private static void RestoreReplayNoFail()
  {
    if (_activeContext?.ReplayNoFailApplied != true || !_activeContext.OriginalNoFailMode.HasValue)
      return;

    ReplayFailPolicy.ApplyReplayNoFail(_activeContext.OriginalNoFailMode.Value);
    _activeContext.ReplayNoFailApplied = false;
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
    if (!TryGetControllerState(out States state) || !IsReplayTimelinePlaybackState(state))
      return;

    player.Tick(nowUs, CurrentGameplayRate(), CurrentWonTimeUs(), ADOBase.controller.paused);
  }

  public static bool TryGetPlaybackSnapshot(out long replayTimeUs, out double timelineRate)
  {
    timelineRate = CurrentTimelineRate();
    return TryComputeReplayTimeUs(out replayTimeUs, out _);
  }

  internal static bool TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot)
  {
    snapshot = default;
    ActiveReplayContext context = _activeContext;
    if (context == null || !ReplayPlaybackCoordinator.IsTimelinePlaying)
      return false;
    if (!TryGetControllerState(out States state) || !CanDisplayTimeline(state))
      return false;
    if (!TryComputeReplayTimeUs(out long elapsedTimeUs, out _))
      return false;

    long durationTimeUs = TimelineDurationTimeUs(context);
    elapsedTimeUs = Math.Max(0L, Math.Min(elapsedTimeUs, durationTimeUs));
    bool canTogglePause = state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl;
    bool canSeek = state == States.PlayerControl && durationTimeUs > 0L && !_timelineRestartPending;
    snapshot = new ReplayTimelinePlaybackSnapshot(
      context.RunId,
      elapsedTimeUs,
      durationTimeUs,
      ADOBase.controller != null && ADOBase.controller.paused,
      canTogglePause,
      canSeek
    );
    return true;
  }

  internal static void TryTogglePauseFromTimeline()
  {
    if (!TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot) || !snapshot.CanTogglePause)
      return;
    if (_timelineScrubActive || _timelineRestartPending)
      return;

    if (!TryComputeReplayTimeUs(out long replayTimeUs, out _))
      return;

    TrySetTimelinePaused(!snapshot.Paused, replayTimeUs);
  }

  internal static void TrySeekTimelineRelative(int deltaSeconds)
  {
    if (_timelineScrubActive || _timelineRestartPending || deltaSeconds == 0)
      return;
    if (!TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot) || !snapshot.CanSeek)
      return;

    long deltaUs = (long)deltaSeconds * 1_000_000L;
    long targetTimeUs = ClampTimelineSeekTime(snapshot.ElapsedTimeUs + deltaUs, snapshot.DurationTimeUs);
    if (!BeginTimelineScrub())
      return;

    CommitTimelineScrubAt(targetTimeUs);
  }

  internal static bool BeginTimelineScrub()
  {
    if (_timelineScrubActive)
      return true;
    if (_timelineRestartPending)
      return false;
    if (!TryGetTimelineSnapshot(out ReplayTimelinePlaybackSnapshot snapshot) || !snapshot.CanSeek)
      return false;

    _timelineScrubActive = true;
    _timelineScrubWasPaused = snapshot.Paused;
    _timelineScrubOriginTimeUs = snapshot.ElapsedTimeUs;
    if (snapshot.Paused)
    {
      SuspendReplayAt(snapshot.ElapsedTimeUs);
      return true;
    }

    if (TrySetTimelinePaused(true, snapshot.ElapsedTimeUs))
      return true;

    ResetTimelineScrubState();
    return false;
  }

  internal static void CommitTimelineScrub(float normalized)
  {
    if (!_timelineScrubActive)
      return;

    long durationTimeUs = TimelineDurationTimeUs(_activeContext);
    long targetTimeUs = ClampTimelineSeekTime(
      (long)Math.Round(Mathf.Clamp01(normalized) * durationTimeUs),
      durationTimeUs
    );
    CommitTimelineScrubAt(targetTimeUs);
  }

  private static void CommitTimelineScrubAt(long targetTimeUs)
  {
    if (!_timelineScrubActive)
      return;

    bool wasPaused = _timelineScrubWasPaused;
    long originTimeUs = _timelineScrubOriginTimeUs;
    if (!TryRestartActiveReplayAt(targetTimeUs, wasPaused))
    {
      Main.Instance?.Log("[ReplayTimeline] Native checkpoint restart failed; keeping the current playback position.");
      ResetTimelineScrubState();
      TrySetTimelinePaused(wasPaused, originTimeUs);
      return;
    }

    ResetTimelineScrubState();
  }

  internal static void CancelTimelineScrub()
  {
    if (!_timelineScrubActive)
      return;

    bool wasPaused = _timelineScrubWasPaused;
    long originTimeUs = _timelineScrubOriginTimeUs;
    ResetTimelineScrubState();
    if (!wasPaused)
      TrySetTimelinePaused(false, originTimeUs);
  }

  public static void UpdateActiveMicrophoneLatency(int latencyMs)
  {
    IReplayMicrophonePlayer player = _activeContext?.MicrophonePlayer;
    if (player == null || !TryComputeReplayTimeUs(out long replayTimeUs, out _))
      return;
    player.UpdateLatency(latencyMs, replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
  }

  public static void UpdateActiveMicrophoneVolume(int volumeDb) =>
    _activeContext?.MicrophonePlayer?.UpdateVolume(volumeDb);

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

  public static void ApplyReplayJudgmentDifficultyNow()
  {
    if (_activeContext?.JudgmentDifficulty.HasValue != true)
      return;
    if (_activeContext.ReplayJudgmentDifficultyApplied)
      return;

    Difficulty difficulty = (Difficulty)(int)_activeContext.JudgmentDifficulty.Value;
    if (!Enum.IsDefined(typeof(Difficulty), difficulty))
      return;

    _activeContext.OriginalJudgmentDifficulty = (int)GCS.difficulty;
    GCS.difficulty = difficulty;
    _activeContext.ReplayJudgmentDifficultyApplied = true;
  }

  private static void RestoreReplayPitch()
  {
    if (_activeContext?.ReplayPitchApplied != true || !_activeContext.OriginalLevelPitchPercent.HasValue)
      return;

    ReplayPitchService.ApplyToEditorLevelData(_activeContext.OriginalLevelPitchPercent.Value);
    _activeContext.ReplayPitchApplied = false;
  }

  private static void RestoreReplayJudgmentDifficulty()
  {
    if (_activeContext?.ReplayJudgmentDifficultyApplied != true || !_activeContext.OriginalJudgmentDifficulty.HasValue)
      return;

    GCS.difficulty = (Difficulty)_activeContext.OriginalJudgmentDifficulty.Value;
    _activeContext.ReplayJudgmentDifficultyApplied = false;
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

  private static bool IsReplayTimelinePlaybackState(States state)
  {
    return state == States.Countdown
      || state == States.Checkpoint
      || state == States.PlayerControl
      || state == States.Won;
  }

  private static bool CanDisplayTimeline(States state)
  {
    return state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl;
  }

  internal static long TimelineDurationTimeUs(ActiveReplayContext context)
  {
    if (context == null)
      return 0L;
    if (
      string.Equals(context.Result, "cleared", StringComparison.OrdinalIgnoreCase)
      && context.Meta?.wonTimeUs is long wonTimeUs
      && wonTimeUs > 0L
    )
      return wonTimeUs;
    return Math.Max(0L, context.TerminalTimeUs);
  }

  internal static long ClampTimelineSeekTime(long targetTimeUs, long durationTimeUs)
  {
    long maximum = Math.Max(0L, durationTimeUs - 1L);
    return Math.Max(0L, Math.Min(targetTimeUs, maximum));
  }

  internal static float ToNormalizedTimelineTime(long replayTimeUs, long durationTimeUs)
  {
    if (durationTimeUs <= 0L)
      return 0f;
    return Mathf.Clamp01((float)((double)replayTimeUs / durationTimeUs));
  }

  private static bool TrySetTimelinePaused(bool paused, long replayTimeUs)
  {
    scrController controller = ADOBase.controller;
    if (controller == null)
      return false;
    if (controller.paused == paused)
    {
      if (paused)
        SuspendReplayAt(replayTimeUs);
      else
      {
        RestoreTimelineEditorPauseState();
        ResumeReplayAt(CurrentReplayTimeOrFallback(replayTimeUs));
      }
      return true;
    }

    if (paused)
    {
      CaptureTimelineEditorPauseState();
      SuspendReplayAt(replayTimeUs);
      SetEditorPausedInPlayMode(true);
      if (controller.TogglePauseGame())
        return true;

      RestoreTimelineEditorPauseState();
      ResumeReplayAt(replayTimeUs);
      return false;
    }

    if (controller.TogglePauseGame())
      return false;

    RestoreTimelineEditorPauseState();
    ResumeReplayAt(CurrentReplayTimeOrFallback(replayTimeUs));
    return true;
  }

  private static long CurrentReplayTimeOrFallback(long fallbackTimeUs)
  {
    ActiveReplayContext context = _activeContext;
    scrConductor conductor = ADOBase.conductor;
    if (
      context?.Meta?.gameplayStartSongPosition == null
      || conductor == null
      || conductor.song == null
      || conductor.song.pitch <= 0f
    )
      return fallbackTimeUs;

    double songPosition;
    if (!GCS.d_oldConductor && !GCS.d_webglConductor)
    {
      songPosition =
        (conductor.dspTime - conductor.dspTimeSong - scrConductor.calibration_i) * conductor.song.pitch
        - conductor.addoffset;
    }
    else
    {
      songPosition = conductor.song.time - scrConductor.calibration_i - conductor.addoffset / conductor.song.pitch;
    }

    double replayTimeUs = (songPosition - context.Meta.gameplayStartSongPosition.Value) * 1_000_000d;
    if (double.IsNaN(replayTimeUs) || double.IsInfinity(replayTimeUs))
      return fallbackTimeUs;
    if (replayTimeUs >= long.MaxValue)
      return long.MaxValue;
    if (replayTimeUs <= long.MinValue)
      return long.MinValue;
    return (long)replayTimeUs;
  }

  private static bool TryRestartActiveReplayAt(long targetTimeUs, bool pauseAtPlayerControl)
  {
    ActiveReplayContext context = _activeContext;
    scnGame customLevel = ADOBase.customLevel;
    scrController controller = ADOBase.controller;
    var floors = ADOBase.lm?.listFloors;
    if (
      context?.Meta?.gameplayStartSongPosition == null
      || customLevel == null
      || controller == null
      || floors == null
      || floors.Count < 2
    )
      return false;
    if (!TryGetControllerState(out States state) || state != States.PlayerControl)
      return false;

    long durationTimeUs = TimelineDurationTimeUs(context);
    targetTimeUs = ClampTimelineSeekTime(targetTimeUs, durationTimeUs);
    double targetSongTime = context.Meta.gameplayStartSongPosition.Value + targetTimeUs / 1_000_000d;
    int floorIndex = FindTimelineSeekFloor(targetSongTime);
    if (floorIndex < 1)
      floorIndex = 1;
    if (floorIndex >= floors.Count)
      floorIndex = floors.Count - 1;
    if (floors[floorIndex].entryTime > targetSongTime && floorIndex > 1)
      floorIndex--;

    context.NativeInputPlayer?.SkipTo(targetTimeUs);
    context.MicrophonePlayer?.Stop();
    ResetReplayHeldInputState();
    PrepareReplayRunRestart("timeline_native_checkpoint_restart");

    _timelineRestartPending = true;
    _timelineRestartPauseAtPlayerControl = pauseAtPlayerControl;
    try
    {
      // Timeline preview pauses through TogglePauseGame, which also sets Time.timeScale to zero.
      // scnGame.Play only clears controller.paused, so resume through the native path before reset or
      // the checkpoint lead-in never advances.
      RestoreTimelineEditorPauseState();
      if (controller.paused && controller.TogglePauseGame())
        throw new InvalidOperationException("ADOFAI refused to resume before the checkpoint restart.");

      GCS.checkpointNum = floorIndex;
      customLevel.ResetScene(isResetCustomLevel: false);
      if (customLevel.Play(floorIndex, remakeFloors: false))
        return true;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("ReplaySessionService.TryRestartActiveReplayAt", exception);
    }

    _timelineRestartPending = false;
    _timelineRestartPauseAtPlayerControl = false;
    return false;
  }

  private static void CompleteTimelineRestart()
  {
    if (!_timelineRestartPending || _activeContext == null)
      return;

    bool pauseAtPlayerControl = _timelineRestartPauseAtPlayerControl;
    scrUIController.instance?.SetToTransparent();
    scrUIController.instance?.txtCountdown?.GetComponent<scrCountdown>()?.CancelGo();
    if (ADOBase.controller != null)
      ADOBase.controller.goShown = true;
    _timelineRestartPending = false;
    _timelineRestartPauseAtPlayerControl = false;
    if (!TryComputeReplayTimeUs(out long replayTimeUs, out _))
      return;

    _activeContext.NativeInputPlayer?.ResetTo(replayTimeUs, CurrentTimelineRate());
    _activeContext.MicrophonePlayer?.ResetTo(replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
    _activeContext.HitContextPlayer?.ResetToAndRebuildJudgments(ADOBase.controller, skipPassedAngles: true);
    ResetReplayHeldInputState();
    _playbackPauseSuspended = false;
    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(replayTimeUs);
    if (pauseAtPlayerControl)
      TrySetTimelinePaused(true, replayTimeUs);
  }

  private static int FindTimelineSeekFloor(double targetSongTime)
  {
    int low = 1;
    int high = ADOBase.lm.listFloors.Count - 1;
    while (low < high)
    {
      int middle = low + (high - low) / 2;
      if (ADOBase.lm.listFloors[middle].entryTime < targetSongTime)
        low = middle + 1;
      else
        high = middle;
    }
    return low;
  }

  private static void SuspendReplayAt(long replayTimeUs)
  {
    if (_activeContext == null || _playbackPauseSuspended)
      return;

    _activeContext.NativeInputPlayer?.SkipTo(replayTimeUs);
    _activeContext.MicrophonePlayer?.ResetTo(replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
    _activeContext.MicrophonePlayer?.Tick(replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs(), paused: true);
    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(replayTimeUs);
    _playbackPauseSuspended = true;
  }

  private static void ResumeReplayAt(long replayTimeUs)
  {
    if (_activeContext == null)
      return;

    _playbackPauseSuspended = false;
    _activeContext.NativeInputPlayer?.ResetTo(replayTimeUs, CurrentTimelineRate());
    _activeContext.MicrophonePlayer?.ResetTo(replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
    _activeContext.MicrophonePlayer?.Tick(replayTimeUs, CurrentGameplayRate(), CurrentWonTimeUs(), paused: false);
    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(replayTimeUs);
  }

  private static void SetEditorPausedInPlayMode(bool paused)
  {
    if (ADOBase.isLevelEditor && scnEditor.instance != null)
      scnEditor.instance.pausedInPlayMode = paused;
  }

  private static void CaptureTimelineEditorPauseState()
  {
    if (_timelinePauseOwnsEditorState || !ADOBase.isLevelEditor || scnEditor.instance == null)
      return;
    _timelineEditorPausedBeforePause = scnEditor.instance.pausedInPlayMode;
    _timelinePauseOwnsEditorState = true;
  }

  private static void RestoreTimelineEditorPauseState()
  {
    if (!_timelinePauseOwnsEditorState)
      return;
    if (ADOBase.isLevelEditor && scnEditor.instance != null)
      scnEditor.instance.pausedInPlayMode = _timelineEditorPausedBeforePause;
    _timelinePauseOwnsEditorState = false;
    _timelineEditorPausedBeforePause = false;
  }

  private static void ResetTimelineScrubState()
  {
    _timelineScrubActive = false;
    _timelineScrubWasPaused = false;
    _timelineScrubOriginTimeUs = 0L;
  }

  private static void ResetTimelineTransportState()
  {
    ResetTimelineScrubState();
    _timelineRestartPending = false;
    _timelineRestartPauseAtPlayerControl = false;
    _timelinePauseOwnsEditorState = false;
    _timelineEditorPausedBeforePause = false;
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
    ReplayPlaybackCoordinator.OnReplayTimeAdvanced(nowUs);
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
