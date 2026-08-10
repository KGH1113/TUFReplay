namespace TUFReplay.Application.Replay;

public static class ReplayClock
{
  /// <summary>
  /// Wall-clock source for the post-clear phase.
  ///
  /// Once the run is won the conductor's song position stops being a usable timeline, so the clock
  /// free-runs from a real-time anchor. That anchor has to follow the virtual clock while a render
  /// is capturing, otherwise the clear screen advances at wall-clock speed inside a simulation
  /// running at a completely different rate and the tail of the video desyncs.
  /// </summary>
  private static double Now =>
    RenderCaptureBridge.IsCapturingActive
      ? UnityEngine.Time.timeAsDouble
      : UnityEngine.Time.realtimeSinceStartupAsDouble;

  public static void EnterWon(ActiveReplayContext context)
  {
    if (context == null || context.WonClockStarted)
      return;

    long fallback = 0L;
    if (context.Meta?.gameplayStartSongPosition != null && ADOBase.conductor != null)
    {
      fallback = (long)(
        (ADOBase.conductor.songposition_minusi - context.Meta.gameplayStartSongPosition.Value) * 1_000_000d
      );
    }

    context.WonClockStartTimeUs = context.Meta?.wonTimeUs ?? fallback;
    context.WonClockStartedAt = Now;
    context.WonClockStarted = true;
  }

  public static bool TryComputeReplayTimeUs(ActiveReplayContext context, out long nowUs, out string reason)
  {
    nowUs = 0L;
    reason = null;

    if (context?.Meta == null)
    {
      reason = "meta_missing";
      return false;
    }

    if (context.WonClockStarted)
    {
      double elapsed = Now - context.WonClockStartedAt;
      nowUs = context.WonClockStartTimeUs + (long)(System.Math.Max(0d, elapsed) * 1_000_000d);
      return true;
    }

    if (ADOBase.conductor == null)
    {
      reason = "conductor_missing";
      return false;
    }

    if (!context.Meta.gameplayStartSongPosition.HasValue)
    {
      reason = "gameplay_start_song_position_missing";
      return false;
    }

    double start = context.Meta.gameplayStartSongPosition.Value;
    nowUs = (long)((ADOBase.conductor.songposition_minusi - start) * 1_000_000d);
    return true;
  }
}
