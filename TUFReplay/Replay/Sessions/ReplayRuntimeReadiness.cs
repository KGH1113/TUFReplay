namespace TUFReplay.Replay.Sessions;

internal readonly struct ReplayRuntimeReadiness
{
  public readonly bool Ready;
  public readonly string Reason;

  public ReplayRuntimeReadiness(bool ready, string reason)
  {
    Ready = ready;
    Reason = reason;
  }
}

internal static class ReplayRuntimeReadinessEvaluator
{
  public static ReplayRuntimeReadiness Evaluate(
    bool hasContext,
    bool hasConductor,
    bool conductorActive,
    bool hasController,
    bool controllerActive,
    bool requiresEditorPlayMode,
    bool editorPlayMode,
    bool conductorTimelineInitialized,
    bool finiteSongPosition,
    bool finiteStartPosition,
    bool playbackState
  )
  {
    if (!hasContext)
      return Blocked("context_missing");
    if (!hasConductor)
      return Blocked("conductor_missing");
    if (!conductorActive)
      return Blocked("conductor_inactive");
    if (!hasController)
      return Blocked("controller_missing");
    if (!controllerActive)
      return Blocked("controller_inactive");
    if (requiresEditorPlayMode && !editorPlayMode)
      return Blocked("editor_not_in_play_mode");
    if (!conductorTimelineInitialized)
      return Blocked("conductor_timeline_uninitialized");
    if (!finiteSongPosition)
      return Blocked("song_position_invalid");
    if (!finiteStartPosition)
      return Blocked("gameplay_start_song_position_invalid");
    if (!playbackState)
      return Blocked("controller_state_not_playback");
    return new ReplayRuntimeReadiness(true, null);
  }

  private static ReplayRuntimeReadiness Blocked(string reason) => new ReplayRuntimeReadiness(false, reason);
}
