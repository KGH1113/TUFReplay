using TUFReplay.Replay.Sessions;

namespace TUFReplay.Replay.Transport;

public static class ReplayClock
{
  public static void EnterWon(ActiveReplayContext context)
  {
    if (context == null || context.WonClockStarted)
      return;

    long fallback = TryComputeReplayTimeUs(context, out long replayTimeUs, out _) ? replayTimeUs : 0L;

    context.WonClockStartTimeUs = context.Meta?.wonTimeUs ?? fallback;
    context.WonClockStartedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
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
      double elapsed = UnityEngine.Time.realtimeSinceStartupAsDouble - context.WonClockStartedAt;
      nowUs = context.WonClockStartTimeUs + (long)(System.Math.Max(0d, elapsed) * 1_000_000d);
      return true;
    }

    if (!TryComputeRawReplayTimeUs(context, out long rawTimeUs, out reason))
      return false;

    nowUs = ApplyRuntimeOffset(context, rawTimeUs);
    return true;
  }

  public static bool TryComputeRawReplayTimeUs(ActiveReplayContext context, out long nowUs, out string reason)
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

    if (!ADOBase.conductor.gameObject.activeInHierarchy)
    {
      reason = "conductor_inactive";
      return false;
    }

    if (
      !ADOBase.conductor.hasSongStarted
      || ADOBase.conductor.song == null
      || ADOBase.conductor.crotchetAtStart <= 0d
      || ADOBase.conductor.song.pitch <= 0f
      || float.IsNaN(ADOBase.conductor.song.pitch)
      || float.IsInfinity(ADOBase.conductor.song.pitch)
    )
    {
      reason = "conductor_timeline_uninitialized";
      return false;
    }

    if (!context.Meta.gameplayStartSongPosition.HasValue)
    {
      reason = "gameplay_start_song_position_missing";
      return false;
    }

    double start = context.Meta.gameplayStartSongPosition.Value;
    double songPosition = ADOBase.conductor.songposition_minusi;
    if (
      double.IsNaN(start)
      || double.IsInfinity(start)
      || double.IsNaN(songPosition)
      || double.IsInfinity(songPosition)
    )
    {
      reason = "song_position_invalid";
      return false;
    }

    nowUs = (long)((songPosition - start) * 1_000_000d);
    return true;
  }

  public static void AlignRuntimeTime(ActiveReplayContext context, long rawTimeUs, long targetTimeUs)
  {
    if (context == null)
      return;
    context.ReplayClockOffsetUs = rawTimeUs - targetTimeUs;
    context.ReplayClockOffsetInitialized = true;
  }

  public static long ApplyRuntimeOffset(ActiveReplayContext context, long rawTimeUs) =>
    context?.ReplayClockOffsetInitialized == true ? rawTimeUs - context.ReplayClockOffsetUs : rawTimeUs;
}
