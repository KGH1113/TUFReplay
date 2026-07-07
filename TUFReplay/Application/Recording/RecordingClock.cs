namespace TUFReplay.Application.Recording;

public static class RecordingClock
{
  public static double CurrentSongPosition()
  {
    return ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
  }

  public static long ToRecordTimeUs(double songPosition, double? gameplayStartSongPosition)
  {
    double start = gameplayStartSongPosition ?? songPosition;
    return (long)((songPosition - start) * 1_000_000d);
  }
}
