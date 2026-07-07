namespace TUFReplay.Application.Replay;

public static class ReplayClock
{
  public static bool TryComputeReplayTimeUs(ActiveReplayContext context, out long nowUs, out string reason)
  {
    nowUs = 0L;
    reason = null;

    if (context?.Meta == null)
    {
      reason = "meta_missing";
      return false;
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
