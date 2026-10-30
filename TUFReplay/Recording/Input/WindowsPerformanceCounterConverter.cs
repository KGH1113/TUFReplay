using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

// Maps QueryPerformanceCounter values from TUFReplayNative.dll to Stopwatch ticks.
// Unity's Mono does not return raw QPC from Stopwatch on Windows: it counts 100 ns
// ticks from the runtime's first clock read, so a raw QPC value is ahead by the
// system uptime at game launch. Mono derives Stopwatch linearly from QPC, so one
// origin pair sampled when capture starts stays exact for the whole session.
internal sealed class WindowsPerformanceCounterConverter
{
  private const int CalibrationSamples = 5;
  private readonly long _counterOrigin;
  private readonly long _stopwatchOrigin;
  private readonly long _counterFrequency;
  private readonly long _stopwatchFrequency;

  internal static WindowsPerformanceCounterConverter CaptureSystemClock()
  {
    if (!QueryPerformanceFrequency(out long frequency) || frequency <= 0)
      throw new InvalidOperationException("QueryPerformanceFrequency failed.");

    // Keep the tightest bracket so a preemption between the reads cannot shift
    // every recorded input.
    long counterOrigin = 0;
    long stopwatchOrigin = 0;
    long narrowest = long.MaxValue;
    for (int i = 0; i < CalibrationSamples; i++)
    {
      long before = Stopwatch.GetTimestamp();
      if (!QueryPerformanceCounter(out long counter))
        throw new InvalidOperationException("QueryPerformanceCounter failed.");
      long after = Stopwatch.GetTimestamp();
      long width = after - before;
      if (width >= narrowest)
        continue;
      narrowest = width;
      counterOrigin = counter;
      stopwatchOrigin = before + width / 2L;
    }
    return new WindowsPerformanceCounterConverter(counterOrigin, stopwatchOrigin, frequency, Stopwatch.Frequency);
  }

  internal WindowsPerformanceCounterConverter(
    long counterOrigin,
    long stopwatchOrigin,
    long counterFrequency,
    long stopwatchFrequency
  )
  {
    if (counterFrequency <= 0)
      throw new ArgumentOutOfRangeException(nameof(counterFrequency));
    if (stopwatchFrequency <= 0)
      throw new ArgumentOutOfRangeException(nameof(stopwatchFrequency));
    _counterOrigin = counterOrigin;
    _stopwatchOrigin = stopwatchOrigin;
    _counterFrequency = counterFrequency;
    _stopwatchFrequency = stopwatchFrequency;
  }

  public long ToStopwatchTicks(long counter)
  {
    // Convert whole seconds and the remainder separately: exact integer math that
    // cannot overflow however long the session runs.
    long delta = counter - _counterOrigin;
    long seconds = delta / _counterFrequency;
    long remainder = delta % _counterFrequency;
    return _stopwatchOrigin + seconds * _stopwatchFrequency + remainder * _stopwatchFrequency / _counterFrequency;
  }

  [DllImport("kernel32.dll")]
  private static extern bool QueryPerformanceCounter(out long value);

  [DllImport("kernel32.dll")]
  private static extern bool QueryPerformanceFrequency(out long value);
}
