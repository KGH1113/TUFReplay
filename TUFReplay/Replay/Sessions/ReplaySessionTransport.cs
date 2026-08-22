using System;
using System.Collections.Generic;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Timeline;
using UnityEngine;

namespace TUFReplay.Replay.Sessions;

public static partial class ReplaySessionService
{
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

  internal static bool TryGetTimelineJudgments(out ReplayTimelineJudgmentSnapshot[] judgments)
  {
    judgments = Array.Empty<ReplayTimelineJudgmentSnapshot>();
    ActiveReplayContext context = _activeContext;
    List<ReplayHitContext> hitContexts = context?.HitContexts;
    if (context == null || hitContexts == null)
      return false;
    if (hitContexts.Count == 0)
      return true;

    long durationTimeUs = TimelineDurationTimeUs(context);
    List<ReplayTimelineJudgmentSnapshot> resolved = new List<ReplayTimelineJudgmentSnapshot>(hitContexts.Count);
    List<scrFloor> floors = ADOBase.lm?.listFloors;
    scrConductor conductor = ADOBase.conductor;
    double? gameplayStartSongPosition = context.Meta?.gameplayStartSongPosition;

    for (int i = 0; i < hitContexts.Count; i++)
    {
      ReplayHitContext hitContext = hitContexts[i];
      HitMargin hitMargin = ReplayHitContextPlayer.ResolveHitMargin(ADOBase.controller, hitContext);
      if (!ReplayTimelineJudgmentMath.TryMapHitMargin(hitMargin, out ReplayTimelineJudgmentKind kind))
        continue;

      long timeUs;
      if (hitContext.TimeUs.HasValue)
      {
        timeUs = Math.Max(0L, Math.Min(hitContext.TimeUs.Value, durationTimeUs));
      }
      else
      {
        if (
          floors == null
          || hitContext.CurrentFloorID < 0
          || hitContext.CurrentFloorID >= floors.Count
          || floors[hitContext.CurrentFloorID]?.nextfloor == null
          || conductor == null
          || conductor.song == null
          || !gameplayStartSongPosition.HasValue
          || !ReplayTimelineJudgmentMath.TryEstimateLegacyTimeUs(
            floors[hitContext.CurrentFloorID].nextfloor.entryTime,
            gameplayStartSongPosition.Value,
            hitContext.CurrAngle,
            conductor.bpm,
            floors[hitContext.CurrentFloorID].speed,
            conductor.song.pitch,
            durationTimeUs,
            out timeUs
          )
        )
          continue;
      }

      resolved.Add(new ReplayTimelineJudgmentSnapshot(timeUs, kind));
    }

    judgments = resolved.ToArray();
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
      NormalizeTimelineFloorLighting(ADOBase.lm?.listFloors, floorIndex);
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

  private static void NormalizeTimelineFloorLighting(List<scrFloor> floors, int targetFloorIndex)
  {
    if (floors == null)
      return;

    int tileFlashStyle = scrVfx.instance != null ? (int)scrVfx.instance.tileFlashStyle : -1;
    for (int i = 0; i < floors.Count; i++)
    {
      scrFloor floor = floors[i];
      if (floor == null)
        continue;

      bool hasLit = floor.seqID <= targetFloorIndex;
      floor.hasLit = hasLit;
      if (floor.topGlow != null)
      {
        floor.topGlow.gameObject.SetActive(ShouldTimelineTopGlowBeActive(hasLit, tileFlashStyle));
      }
    }
  }

  internal static bool ShouldTimelineTopGlowBeActive(bool hasLit, int tileFlashStyle)
  {
    if (tileFlashStyle == (int)TileFlashStyle.AlwaysOn)
      return true;
    if (tileFlashStyle == (int)TileFlashStyle.AlwaysBlack)
      return false;
    return hasLit;
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
}
