using System;
using System.Diagnostics;

namespace TUFReplay.Webcam.Timing;

// Pairs the wall clock FFmpeg stamps with -use_wallclock_as_timestamps with the
// Stopwatch clock used for input. The Windows camera relay converts each frame.
public sealed class WebcamWallClock
{
  public WebcamWallClock(long unixUs, long timestamp)
  {
    UnixUs = unixUs;
    Timestamp = timestamp;
  }

  public long UnixUs { get; }
  public long Timestamp { get; }

  public static WebcamWallClock Capture()
  {
    long before = Stopwatch.GetTimestamp();
    long unixUs = (DateTime.UtcNow.Ticks - 621355968000000000L) / 10;
    long after = Stopwatch.GetTimestamp();
    return new WebcamWallClock(unixUs, before + (after - before) / 2);
  }
}
