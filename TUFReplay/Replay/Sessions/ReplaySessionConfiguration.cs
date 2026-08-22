using System;
using System.Collections.Generic;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Transport;
using UnityEngine;

namespace TUFReplay.Replay.Sessions;

public static partial class ReplaySessionService
{
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
}
