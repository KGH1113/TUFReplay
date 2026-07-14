namespace TUFReplay.Application.Recording;

public static class RecordingClock
{
  public const string HybridInputTimeBase = "conductor-songposition-then-unscaled-monotonic";

  public static double CurrentSongPosition()
  {
    return ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
  }

  public static double CurrentUnscaledTime()
  {
    return UnityEngine.Time.unscaledTimeAsDouble;
  }

  public static long ToRecordTimeUs(double songPosition, double? gameplayStartSongPosition)
  {
    double start = gameplayStartSongPosition ?? songPosition;
    return (long)((songPosition - start) * 1_000_000d);
  }

  public static long ContinueFromUnscaledTime(long anchorTimeUs, double anchorUnscaledTime, double currentUnscaledTime)
  {
    double elapsedSeconds = System.Math.Max(0d, currentUnscaledTime - anchorUnscaledTime);
    return anchorTimeUs + (long)(elapsedSeconds * 1_000_000d);
  }
}
