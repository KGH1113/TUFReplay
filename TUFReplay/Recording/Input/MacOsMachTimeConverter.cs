using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal sealed class MacOsMachTimeConverter
{
  private readonly ulong _machOrigin;
  private readonly long _stopwatchOrigin;
  private readonly double _nanosecondsPerMachTick;

  // The microphone helper reports CoreMedia host times in mach_absolute_time
  // units. Sample the same host clock locally; IPC delivery time is irrelevant.
  internal static MacOsMachTimeConverter CaptureSystemClock()
  {
    if (MachTimebaseInfo(out MachTimebase timebase) != 0 || timebase.Numerator == 0 || timebase.Denominator == 0)
      throw new InvalidOperationException("Invalid mach timebase.");
    long before = Stopwatch.GetTimestamp();
    ulong machOrigin = MachAbsoluteTime();
    long after = Stopwatch.GetTimestamp();
    return new MacOsMachTimeConverter(
      machOrigin,
      before + (after - before) / 2L,
      timebase.Numerator,
      timebase.Denominator
    );
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

  internal ulong ToMachTimestamp(long stopwatchTicks)
  {
    if (stopwatchTicks <= 0)
      throw new ArgumentOutOfRangeException(nameof(stopwatchTicks));
    double delta = (stopwatchTicks - _stopwatchOrigin) * 1_000_000_000d / Stopwatch.Frequency / _nanosecondsPerMachTick;
    return delta >= 0
      ? checked(_machOrigin + (ulong)Math.Round(delta))
      : checked(_machOrigin - (ulong)Math.Round(-delta));
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MachTimebase
  {
    public uint Numerator;
    public uint Denominator;
  }

  [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_absolute_time")]
  private static extern ulong MachAbsoluteTime();

  [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_timebase_info")]
  private static extern int MachTimebaseInfo(out MachTimebase info);
}
