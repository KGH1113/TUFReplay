using System;
using System.Diagnostics;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsMachTimeConverter
{
  private readonly ulong _machOrigin;
  private readonly long _stopwatchOrigin;
  private readonly double _nanosecondsPerMachTick;

  public MacOsMachTimeConverter(MacOsIoHidNativeLibrary library)
  {
    if (library == null)
      throw new ArgumentNullException(nameof(library));
    library.GetTimebase(out uint numerator, out uint denominator);
    if (numerator == 0 || denominator == 0)
      throw new InvalidOperationException("Invalid mach timebase.");
    long before = Stopwatch.GetTimestamp();
    _machOrigin = library.MachNow();
    long after = Stopwatch.GetTimestamp();
    _stopwatchOrigin = before + (after - before) / 2;
    _nanosecondsPerMachTick = (double)numerator / denominator;
  }

  internal MacOsMachTimeConverter(ulong machOrigin, long stopwatchOrigin, uint numerator, uint denominator)
  {
    _machOrigin = machOrigin;
    _stopwatchOrigin = stopwatchOrigin;
    _nanosecondsPerMachTick = (double)numerator / denominator;
  }

  public long ToStopwatchTicks(ulong machTimestamp)
  {
    double deltaMach =
      machTimestamp >= _machOrigin ? machTimestamp - _machOrigin : -(double)(_machOrigin - machTimestamp);
    double deltaStopwatch = deltaMach * _nanosecondsPerMachTick * Stopwatch.Frequency / 1_000_000_000d;
    return _stopwatchOrigin + (long)Math.Round(deltaStopwatch);
  }

  public long ToNanoseconds(ulong machTimestamp)
  {
    double value = machTimestamp * _nanosecondsPerMachTick;
    return value >= long.MaxValue ? long.MaxValue : (long)Math.Round(value);
  }
}
