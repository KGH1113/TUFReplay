using System;
using System.Diagnostics;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsEventTimeConverter
{
  private readonly ulong _originNs;
  private readonly long _originTicks;

  internal MacOsEventTimeConverter(MacOsCGEventNativeLibrary library)
  {
    long before = Stopwatch.GetTimestamp();
    _originNs = library.ClockNowNs();
    long after = Stopwatch.GetTimestamp();
    _originTicks = before + (after - before) / 2;
  }

  internal MacOsEventTimeConverter(ulong originNs, long originTicks)
  {
    _originNs = originNs;
    _originTicks = originTicks;
  }

  public long ToStopwatchTicks(ulong timestampNs)
  {
    // Subtract in integer space before converting; a long-running host clock
    // must not lose submillisecond precision by converting its absolute value.
    double delta = timestampNs >= _originNs ? timestampNs - _originNs : -(double)(_originNs - timestampNs);
    return _originTicks + (long)Math.Round(delta * Stopwatch.Frequency / 1_000_000_000d);
  }
}
