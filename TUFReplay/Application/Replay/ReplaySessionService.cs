using System;
using System.Reflection;
using ADOFAI;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Features.Replay;
using TUFReplay.Infrastructure.NativeInput;
using TUFReplay.Infrastructure.Unity;
using UnityEngine;

namespace TUFReplay.Application.Replay;

public static class ReplaySessionService
{
  private static readonly FieldInfo PlanetCosmeticAngleTweenField = typeof(scrPlanet).GetField(
    "_cosmeticAngleTween",
    BindingFlags.Instance | BindingFlags.NonPublic
  );
  private static readonly MethodInfo PlanetCosmeticAngleTweenKillMethod = PlanetCosmeticAngleTweenField
    ?.FieldType.Assembly.GetType("DG.Tweening.TweenExtensions")
    ?.GetMethod(
      "Kill",
      BindingFlags.Public | BindingFlags.Static,
      binder: null,
      types: new[] { PlanetCosmeticAngleTweenField.FieldType, typeof(bool) },
      modifiers: null
    );
  private static ActiveReplayContext _activeContext;
  private static int _pendingReplayPitchApplyFrame = -1;
  private static bool _suppressReplayMarkFail;
  private static bool _playbackPauseSuspended;
  private static bool _timelineScrubActive;
  private static bool _timelineScrubWasPaused;
  private static long _timelineScrubOriginTimeUs;
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
    bool canSeek = state == States.PlayerControl && durationTimeUs > 0L;
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
    if (_timelineScrubActive)
      return;

    if (!TryComputeReplayTimeUs(out long replayTimeUs, out _))
      return;

    TrySetTimelinePaused(!snapshot.Paused, replayTimeUs);
  }

  internal static void TrySeekTimelineRelative(int deltaSeconds)
  {
    if (_timelineScrubActive || deltaSeconds == 0)
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

    if (!TrySeekActiveReplayTo(targetTimeUs))
    {
      Main.Instance?.Log("[ReplayTimeline] Seek failed; restoring the previous playback position.");
      TrySeekActiveReplayTo(originTimeUs);
      targetTimeUs = originTimeUs;
    }

    ResetTimelineScrubState();
    if (!wasPaused)
      TrySetTimelinePaused(false, targetTimeUs);
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

  private static bool TrySeekActiveReplayTo(long targetTimeUs)
  {
    ActiveReplayContext context = _activeContext;
    scrController controller = ADOBase.controller;
    scrConductor conductor = ADOBase.conductor;
    if (
      context?.Meta?.gameplayStartSongPosition == null
      || controller == null
      || conductor == null
      || ADOBase.lm?.listFloors == null
      || ADOBase.lm.listFloors.Count < 2
    )
      return false;
    if (!TryGetControllerState(out States state) || state != States.PlayerControl)
      return false;

    long durationTimeUs = TimelineDurationTimeUs(context);
    targetTimeUs = ClampTimelineSeekTime(targetTimeUs, durationTimeUs);
    double targetSongTime = context.Meta.gameplayStartSongPosition.Value + targetTimeUs / 1_000_000d;
    int floorIndex = FindTimelineSeekFloor(targetSongTime);
    if (floorIndex < 1)
      return false;
    if (ADOBase.lm.listFloors[floorIndex].entryTime > targetSongTime && floorIndex > 1)
      floorIndex--;
    int litThroughFloorIndex =
      ADOBase.lm.listFloors[floorIndex].entryTime <= targetSongTime ? floorIndex : floorIndex - 1;

    double pitch = conductor.song != null && conductor.song.pitch > 0f ? conductor.song.pitch : 1d;

    bool audioListenerPaused = AudioListener.pause;
    try
    {
      context.NativeInputPlayer?.SkipTo(targetTimeUs);
      ResetReplayHeldInputState();
      AudioListener.pause = true;

      RestoreTimelineTwirlState();
      controller.Scrub(floorIndex, forceDontStartMusicFourTilesBefore: true);
      // scrController.Scrub resumes the listener internally after rebuilding its floor-level state.
      // Keep the rest of this transaction silent until every timeline consumer has the same clock.
      AudioListener.pause = true;

      // Scrub internally advances multi-planet midspins and rewinds free-roam floors. Use the floor it
      // actually selected instead of placing the planets again from the unnormalized request.
      int restoredFloorIndex = controller.currFloor != null ? controller.currFloor.seqID : floorIndex;
      if (restoredFloorIndex >= 1 && restoredFloorIndex < ADOBase.lm.listFloors.Count)
        floorIndex = restoredFloorIndex;

      scrFloor targetFloor = ADOBase.lm.listFloors[floorIndex];
      float remainingPauseSeconds = 0f;
      if (targetSongTime >= targetFloor.entryTime && targetSongTime < targetFloor.entryTimeAfterExtraBeats)
      {
        remainingPauseSeconds =
          (float)Math.Max(0d, (targetFloor.entryTimeAfterExtraBeats - targetSongTime) / pitch) + 0.2f;
      }

      RebuildTimelineVfx((float)targetSongTime);
      RestoreTimelineFloorHitVisuals(litThroughFloorIndex);
      ScrubTimelineAudio(conductor, targetSongTime, pitch);
      double snappedLastAngle = 0d;
      double targetExitAngle = 0d;
      bool hasLandingOrbitAngles =
        targetFloor.extraBeats <= 0f
        && !targetFloor.freeroam
        && TryGetTimelineLandingOrbitAngles(context, floorIndex, out snappedLastAngle, out targetExitAngle);

      foreach (scrPlayer player in ADOBase.playerManager)
      {
        if (player == null)
          continue;

        // Between hits the chosen planet is anchored to the last floor it landed on. The game's scrub
        // path reconstructs its orbit from floor.entryangle, while a real landing carries the previous
        // targetExitAngle forward. Those formulas diverge after direction changes such as Twirl, so
        // restore the real landing recurrence before evaluating the exact target instant.
        PlanetarySystem planetarySystem = player.planetarySystem;
        if (planetarySystem == null)
          continue;

        ResetTimelinePlanetCosmeticAngles(planetarySystem);
        player.lastHit = targetFloor.entryTime;
        planetarySystem.ScrubToFloorNumber(
          floorIndex,
          windbackTime: null,
          movePos: ADOBase.customLevel != null || RDC.debug
        );

        // The game's scrub path only assigns cosmeticRadius when the landed floor's scale is not 1,
        // so a previous scaled floor can leak its radius into a later normal floor. It also assigns
        // the landed radius after Update_RefreshAngles has already interpolated toward the next floor.
        // Restore the baseline unconditionally, then evaluate the target instant once more.
        planetarySystem.isCW = !targetFloor.isCCW;
        planetarySystem.speed = targetFloor.speed;
        scrPlanet chosenPlanet = planetarySystem.chosenPlanet;
        if (hasLandingOrbitAngles && chosenPlanet != null)
        {
          chosenPlanet.SetSnappedLastAngle(snappedLastAngle);
          chosenPlanet.SetTargetExitAngle(targetExitAngle);
        }
        if (planetarySystem.planetList != null)
        {
          float radius = controller.tileSize * targetFloor.radiusScale;
          foreach (scrPlanet planet in planetarySystem.planetList)
          {
            if (planet != null)
              planet.cosmeticRadius = radius;
          }
        }
        chosenPlanet?.Update_RefreshAngles();
      }

      foreach (scrPlayer player in ADOBase.playerManager)
      {
        if (player == null)
          continue;
        player.UnlockInput();
        if (remainingPauseSeconds > 0f)
          player.LockInput(remainingPauseSeconds);
      }
      RestoreTimelineCamera(controller);

      context.WonClockStarted = false;
      context.NativeInputPlayer?.SkipTo(targetTimeUs);
      context.MicrophonePlayer?.ResetTo(targetTimeUs, CurrentGameplayRate(), CurrentWonTimeUs());
      context.HitContextPlayer?.ResetToAndRebuildJudgments(controller, skipPassedAngles: true);
      ResetReplayHeldInputState();
      ReplayPlaybackCoordinator.OnReplayTimeAdvanced(targetTimeUs);
      _playbackPauseSuspended = true;
      return true;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("ReplaySessionService.TrySeekActiveReplayTo", exception);
      return false;
    }
    finally
    {
      AudioListener.pause = audioListenerPaused;
    }
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

  private static bool TryGetTimelineLandingOrbitAngles(
    ActiveReplayContext context,
    int targetFloorIndex,
    out double snappedLastAngle,
    out double targetExitAngle
  )
  {
    snappedLastAngle = 0d;
    targetExitAngle = 0d;

    var floors = ADOBase.lm?.listFloors;
    if (context?.HitContexts == null || floors == null || targetFloorIndex < 1 || targetFloorIndex >= floors.Count)
      return false;

    int previousFloorIndex = targetFloorIndex - 1;
    double previousTargetExitAngle = 0d;
    bool foundPreviousTarget = false;
    for (int contextIndex = context.HitContexts.Count - 1; contextIndex >= 0; contextIndex--)
    {
      ReplayHitContext hitContext = context.HitContexts[contextIndex];
      if (hitContext.CurrentFloorID != previousFloorIndex)
        continue;

      previousTargetExitAngle = hitContext.TargetExitAngle;
      foundPreviousTarget = true;
      break;
    }
    if (!foundPreviousTarget)
      return false;

    scrFloor floor = floors[targetFloorIndex];
    if (floor == null)
      return false;

    // ReplayHitContextPlayer applies this exact recorded target immediately before SwitchChosen().
    // MoveToNextFloor() then uses it to seed the newly chosen planet. Reusing the recorded value here
    // avoids a synthetic angle chain that only converges back to the recording after later hits.
    double direction = floor.isCCW ? -1d : 1d;
    double planetOffset = scrMisc.GetInverseAnglePerBeatMultiplanet(floor.numPlanets) * direction;
    scrFloor previousFloor = floor.prevfloor;
    if (previousFloor != null && previousFloor.midSpin && floor.numPlanets > 2)
    {
      double previousDirection = previousFloor.isCCW ? -1d : 1d;
      planetOffset -= 2d * scrMisc.GetInverseAnglePerBeatMultiplanet(previousFloor.numPlanets) * previousDirection;
    }

    const double fullTurn = Math.PI * 2d;
    double landedAngle = previousTargetExitAngle + Math.PI;
    landedAngle -= Math.Floor(landedAngle / fullTurn) * fullTurn;
    snappedLastAngle = landedAngle + planetOffset;
    targetExitAngle = snappedLastAngle + floor.angleLength * direction;
    return true;
  }

  private static void ResetTimelinePlanetCosmeticAngles(PlanetarySystem planetarySystem)
  {
    if (planetarySystem?.planetList == null)
      return;

    for (int planetIndex = 0; planetIndex < planetarySystem.planetList.Count; planetIndex++)
    {
      scrPlanet planet = planetarySystem.planetList[planetIndex];
      if (planet == null)
        continue;

      // MoveToNextFloor normally kills this tween before establishing the next orbit, but the game's
      // ScrubToFloorNumber path does not. A tween retained from the abandoned timeline keeps adding a
      // visual-only angular offset until a later real landing suddenly kills it and appears to fix the
      // orbit. Kill it before scrub; HandlePause may then create a fresh target-position tween.
      object tween = PlanetCosmeticAngleTweenField?.GetValue(planet);
      if (tween != null)
        PlanetCosmeticAngleTweenKillMethod?.Invoke(null, new[] { tween, (object)false });

      PlanetCosmeticAngleTweenField?.SetValue(planet, null);
      planet.cosmeticAngle = 0f;
    }
  }

  private static void RestoreTimelineTwirlState()
  {
    scnGame customLevel = ADOBase.customLevel;
    var floors = ADOBase.lm?.listFloors;
    var events = customLevel?.events;
    if (floors == null || events == null || floors.Count == 0)
      return;

    // Standard Twirl direction is accumulated once while the chart is prepared. Free-roam swirl
    // mutates floor.isCCW in place during play, however, so seeking backward can otherwise reuse a
    // direction belonging to the abandoned future state. Rebuild the authored main-path parity from
    // LevelEvent data before the game's own scrub reads it.
    var twirls = new bool[floors.Count];
    for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
    {
      LevelEvent levelEvent = events[eventIndex];
      if (
        levelEvent != null
        && levelEvent.eventType == LevelEventType.Twirl
        && levelEvent.floor >= 0
        && levelEvent.floor < twirls.Length
      )
      {
        twirls[levelEvent.floor] = !twirls[levelEvent.floor];
      }
    }

    bool isCcw = false;
    for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
    {
      if (twirls[floorIndex])
        isCcw = !isCcw;

      scrFloor floor = floors[floorIndex];
      if (floor == null)
        continue;

      floor.isCCW = isCcw;
      if (!floor.freeroam || floor.freeroamGenerated || floor.freeroamFloors == null)
        continue;

      // Exact free-roam visitation is not recorded. Its generated floors therefore return to the
      // authored region baseline and resume mutating naturally from the committed seek position.
      for (int generatedIndex = 0; generatedIndex < floor.freeroamFloors.Count; generatedIndex++)
      {
        scrFloor generatedFloor = floor.freeroamFloors[generatedIndex];
        if (generatedFloor != null)
          generatedFloor.isCCW = isCcw;
      }
    }
  }

  private static void ScrubTimelineAudio(scrConductor conductor, double targetSongTime, double pitch)
  {
    double inputCalibration = scrConductor.calibration_i;
    double playbackSongTime = targetSongTime + inputCalibration * pitch;
    double immediateDspTimeSong = conductor.dspTime - playbackSongTime / pitch - conductor.addoffset / pitch;

    conductor.ScrubMusicToTime(playbackSongTime);
    if (conductor.song != null && conductor.song.clip != null)
      conductor.song.SetScheduledStartTime(conductor.dspTime);

    // ScrubMusicToTime intentionally schedules 100 ms ahead. Timeline scrub has already paused every
    // consumer, so remove that lead and make the requested replay time the shared resume boundary.
    conductor.dspTimeSong = immediateDspTimeSong;
    conductor.songposition_minusi = targetSongTime;
    foreach (scrPlayer player in ADOBase.playerManager)
    {
      if (player != null)
        player.lastHit = targetSongTime;
    }
    conductor.PlayHitTimes();
  }

  private static void RestoreTimelineCamera(scrController controller)
  {
    scrCamera camera = controller?.camy;
    if (camera == null)
      return;

    camera.UpdateFollowCam(force: true);
    if (camera.followMode && camera.furthestPlanet != null)
      camera.ViewObjectInstant(camera.furthestPlanet.transform, includeOffset: true);
  }

  private static void RebuildTimelineVfx(float targetSongTime)
  {
    scnGame customLevel = ADOBase.customLevel;
    scrVfxPlus vfx = scrVfxPlus.instance;
    var floors = ADOBase.lm?.listFloors;
    if (customLevel == null || vfx == null || floors == null)
      return;

    var originalStartPositions = new Vector3[floors.Count];
    var hasStartPosition = new bool[floors.Count];
    UnityEngine.Random.State randomState = UnityEngine.Random.state;
    Exception cleanupException = null;
    try
    {
      for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
      {
        scrFloor floor = floors[floorIndex];
        if (floor == null)
          continue;

        originalStartPositions[floorIndex] = floor.startPos;
        hasStartPosition[floorIndex] = true;
        if (floor.plusEffects == null)
          continue;

        for (int effectIndex = 0; effectIndex < floor.plusEffects.Count; effectIndex++)
        {
          ffxPlusBase effect = floor.plusEffects[effectIndex];
          if (effect == null)
            continue;

          try
          {
            effect.Kill();
          }
          catch (Exception exception)
          {
            cleanupException ??= exception;
          }

          effect.triggered = false;
        }
      }

      // scrVfxPlus.Reset only resets its dictionaries. The filter components themselves keep the
      // enabled state produced by later events unless the level's canonical reset path disables them.
      customLevel.DisableFilters();

      for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
      {
        scrFloor floor = floors[floorIndex];
        if (floor == null)
          continue;

        RestoreTimelineFloorVisualBaseline(floor);
        if (!floor.freeroam || floor.freeroamGenerated || floor.freeroamFloors == null)
          continue;

        for (int generatedIndex = 0; generatedIndex < floor.freeroamFloors.Count; generatedIndex++)
        {
          scrFloor generatedFloor = floor.freeroamFloors[generatedIndex];
          if (generatedFloor != null)
            RestoreTimelineFloorVisualBaseline(generatedFloor);
        }
      }

      for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
      {
        scrFloor floor = floors[floorIndex];
        if (floor?.plusEffects == null)
          continue;

        for (int effectIndex = 0; effectIndex < floor.plusEffects.Count; effectIndex++)
        {
          if (floor.plusEffects[effectIndex] is not ffxFloorAppearPlus floorAppear)
            continue;

          try
          {
            floorAppear.FloorSetup();
          }
          catch (Exception exception)
          {
            cleanupException ??= exception;
          }
        }
      }

      // PrepVfx always snapshots the current transforms into startPos. FloorSetup deliberately put
      // TrackAppear floors into their pre-appearance transforms, so keep the chart's original anchors.
      customLevel.PrepVfx(0, remakeFloors: false);
      RestoreTimelineFloorStartPositions(floors, originalStartPositions, hasStartPosition);
      vfx.ScrubToTime(targetSongTime);

      if (cleanupException != null)
        Main.Instance?.LogException("ReplaySessionService.RebuildTimelineVfx cleanup", cleanupException);
    }
    finally
    {
      RestoreTimelineFloorStartPositions(floors, originalStartPositions, hasStartPosition);
      UnityEngine.Random.state = randomState;
    }
  }

  private static void RestoreTimelineFloorVisualBaseline(scrFloor floor)
  {
    Transform floorTransform = floor.transform;
    Vector3 rotation = floor.startRot;
    rotation.z += floor.rotationOffset;
    floorTransform.position = floor.startPos;
    floorTransform.eulerAngles = rotation;
    floor.tweenRot = rotation;
    floor.opacity = floor.opacityVal;
    floor.extendAnim = -1f;
    if (!floor.freeroamGenerated)
      floorTransform.localScale = floor.startScale;
    floor.SetTrackStyle(floor.initialTrackStyle);
  }

  private static void RestoreTimelineFloorHitVisuals(int litThroughFloorIndex)
  {
    var floors = ADOBase.lm?.listFloors;
    if (floors == null)
      return;

    TileFlashStyle flashStyle = scrVfx.instance != null ? scrVfx.instance.tileFlashStyle : TileFlashStyle.Rando;
    for (int floorIndex = 0; floorIndex < floors.Count; floorIndex++)
    {
      scrFloor floor = floors[floorIndex];
      if (floor == null)
        continue;

      RestoreTimelineFloorHitVisual(floor, floorIndex <= litThroughFloorIndex, flashStyle);
      if (!floor.freeroam || floor.freeroamGenerated || floor.freeroamFloors == null)
        continue;

      for (int generatedIndex = 0; generatedIndex < floor.freeroamFloors.Count; generatedIndex++)
      {
        scrFloor generatedFloor = floor.freeroamFloors[generatedIndex];
        if (generatedFloor != null)
          RestoreTimelineFloorHitVisual(generatedFloor, lit: false, flashStyle);
      }
    }
  }

  private static void RestoreTimelineFloorHitVisual(scrFloor floor, bool lit, TileFlashStyle flashStyle)
  {
    floor.hasLit = lit;
    bool glowVisible =
      flashStyle == TileFlashStyle.AlwaysOn
      || (
        lit
        && flashStyle != TileFlashStyle.AlwaysBlack
        && flashStyle != TileFlashStyle.MoveToTopLayer
        && !floor.disableGlow
      );

    if (floor.topGlow != null)
      floor.topGlow.gameObject.SetActive(glowVisible);
    if (floor.bottomGlow != null)
      floor.bottomGlow.gameObject.SetActive(glowVisible);
    if (floor.floorRenderer?.renderer != null)
      floor.floorRenderer.renderer.sortingLayerName =
        flashStyle == TileFlashStyle.MoveToTopLayer && lit ? "FloorTop" : "Floor";
  }

  private static void RestoreTimelineFloorStartPositions(
    System.Collections.Generic.IReadOnlyList<scrFloor> floors,
    Vector3[] originalStartPositions,
    bool[] hasStartPosition
  )
  {
    int count = Math.Min(floors.Count, originalStartPositions.Length);
    for (int floorIndex = 0; floorIndex < count; floorIndex++)
    {
      scrFloor floor = floors[floorIndex];
      if (floor != null && hasStartPosition[floorIndex])
        floor.startPos = originalStartPositions[floorIndex];
    }
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
