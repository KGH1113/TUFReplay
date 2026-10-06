using System;
using System.Diagnostics;

namespace TUFReplay.Webcam.Timing;

public sealed class WebcamWallClock
{
  private readonly long _unixUs;
  private readonly long _timestamp;

  public WebcamWallClock(long unixUs, long timestamp)
  {
    _unixUs = unixUs;
    _timestamp = timestamp;
  }

  public static WebcamWallClock Capture()
  {
    long before = Stopwatch.GetTimestamp();
    long unixUs = (DateTime.UtcNow.Ticks - 621355968000000000L) / 10;
    long after = Stopwatch.GetTimestamp();
    return new WebcamWallClock(unixUs, before + (after - before) / 2);
  }

  public long ToTimestamp(long pts, int numerator, int denominator)
  {
    if (numerator <= 0 || denominator <= 0)
      throw new ArgumentOutOfRangeException(nameof(denominator));
    long unixUs = checked((long)Math.Round((decimal)pts * numerator * 1_000_000 / denominator));
    // Subtract the epoch before converting to floating point to retain sub-frame precision.
    return checked(_timestamp + (long)Math.Round((unixUs - _unixUs) * (double)Stopwatch.Frequency / 1_000_000));
  }
}
