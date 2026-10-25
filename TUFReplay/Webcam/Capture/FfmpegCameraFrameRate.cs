using System;
using System.Globalization;
using System.Numerics;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegCameraFrameRate
{
  internal const long TicksPerSecond = 10_000_000;
  internal const int MaximumComponent = 1_001_000;

  private FfmpegCameraFrameRate(long intervalTicks, long numerator, long denominator)
  {
    IntervalTicks = intervalTicks;
    // ':' takes av_parse_ratio's integer path. Both components must also fit
    // av_parse_video_rate's bound, or FFmpeg will approximate even this form.
    Argument =
      numerator.ToString(CultureInfo.InvariantCulture) + ":" + denominator.ToString(CultureInfo.InvariantCulture);
  }

  public long IntervalTicks { get; }
  public double FrameRate => (double)TicksPerSecond / IntervalTicks;
  public string Argument { get; }

  public static FfmpegCameraFrameRate Create(double advertisedFrameRate)
  {
    if (!TryCreate(advertisedFrameRate, out FfmpegCameraFrameRate rate))
      throw new ArgumentOutOfRangeException(nameof(advertisedFrameRate));
    return rate;
  }

  public static bool TryCreate(double advertisedFrameRate, out FfmpegCameraFrameRate rate)
  {
    rate = null;
    if (advertisedFrameRate <= 0 || double.IsNaN(advertisedFrameRate) || double.IsInfinity(advertisedFrameRate))
      return false;
    // The capability listing has limited decimal precision. This reconstructs
    // its nearest interval; it cannot recover precision omitted by the driver
    // listing. Capture still needs a device-default fallback when rejected.
    double interval = Math.Round(TicksPerSecond / advertisedFrameRate);
    if (interval < 1 || interval > TicksPerSecond * MaximumComponent)
      return false;
    return TryFromInterval((long)interval, out rate);
  }

  public static bool TryFromInterval(long intervalTicks, out FfmpegCameraFrameRate rate)
  {
    rate = null;
    if (intervalTicks < 1 || intervalTicks > TicksPerSecond * MaximumComponent)
      return false;

    // DirectShow truncates TicksPerSecond * denominator / numerator. To get
    // exactly intervalTicks, find n/d in (T/(interval+1), T/interval]. An
    // accelerated Stern-Brocot search finds the simplest fraction in that
    // interval using exact integer comparisons, without an epsilon or rounding.
    long lowerNumerator = 0;
    long lowerDenominator = 1;
    long upperNumerator = 1;
    long upperDenominator = 0;
    while (true)
    {
      long numerator = lowerNumerator + upperNumerator;
      long denominator = lowerDenominator + upperDenominator;
      if (numerator > MaximumComponent || denominator > MaximumComponent)
        return false;

      if ((BigInteger)numerator * (intervalTicks + 1) <= (BigInteger)TicksPerSecond * denominator)
      {
        BigInteger steps =
          ((BigInteger)TicksPerSecond * lowerDenominator - (BigInteger)(intervalTicks + 1) * lowerNumerator)
          / ((BigInteger)(intervalTicks + 1) * upperNumerator - (BigInteger)TicksPerSecond * upperDenominator);
        if (!Advance(ref lowerNumerator, ref lowerDenominator, upperNumerator, upperDenominator, steps))
          return false;
      }
      else if ((BigInteger)numerator * intervalTicks > (BigInteger)TicksPerSecond * denominator)
      {
        // The upper endpoint is inclusive, hence subtract one before dividing.
        BigInteger steps =
          ((BigInteger)intervalTicks * upperNumerator - (BigInteger)TicksPerSecond * upperDenominator - 1)
          / ((BigInteger)TicksPerSecond * lowerDenominator - (BigInteger)intervalTicks * lowerNumerator);
        if (!Advance(ref upperNumerator, ref upperDenominator, lowerNumerator, lowerDenominator, steps))
          return false;
      }
      else
      {
        rate = new FfmpegCameraFrameRate(intervalTicks, numerator, denominator);
        return true;
      }
    }
  }

  private static bool Advance(
    ref long numerator,
    ref long denominator,
    long addNumerator,
    long addDenominator,
    BigInteger steps
  )
  {
    long maximumSteps = MaximumComponent;
    if (addNumerator != 0)
      maximumSteps = Math.Min(maximumSteps, (MaximumComponent - numerator) / addNumerator);
    if (addDenominator != 0)
      maximumSteps = Math.Min(maximumSteps, (MaximumComponent - denominator) / addDenominator);
    if (steps < 1 || steps > maximumSteps)
      return false;
    numerator += (long)steps * addNumerator;
    denominator += (long)steps * addDenominator;
    return true;
  }
}
