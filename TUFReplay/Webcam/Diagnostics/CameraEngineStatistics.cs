using System;

namespace TUFReplay.Webcam.Diagnostics;

// Raw counters accept zero, but missing measurements stay null in the log.
internal struct CameraMetricStatistics
{
  internal long Count { get; private set; }
  private double _total;
  private double _min;
  private double _max;

  internal void Record(double value)
  {
    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
      return;
    _min = Count == 0 ? value : Math.Min(_min, value);
    _max = Count == 0 ? value : Math.Max(_max, value);
    _total += value;
    Count++;
  }

  internal object DiagnosticState() =>
    new
    {
      samples = Count,
      total = Count > 0 ? (double?)_total : null,
      mean = Count > 0 ? (double?)(_total / Count) : null,
      min = Count > 0 ? (double?)_min : null,
      max = Count > 0 ? (double?)_max : null,
    };
}

internal sealed class CameraFrameTimingStatistics
{
  private ulong _lastTimestamp;
  private CameraMetricStatistics _cpu;
  private CameraMetricStatistics _mainThread;
  private CameraMetricStatistics _renderThread;
  private CameraMetricStatistics _presentWait;
  private CameraMetricStatistics _gpu;
  private long _duplicates;
  private long _missingTimestamps;

  // Unity can return a delayed frame repeatedly. Keep the timestamp across
  // window resets so old GPU timings cannot appear as new measurements.
  internal void Record(
    ulong timestamp,
    double cpu,
    double mainThread,
    double renderThread,
    double presentWait,
    double gpu
  )
  {
    if (timestamp == 0)
    {
      _missingTimestamps++;
      return;
    }
    if (timestamp <= _lastTimestamp)
    {
      _duplicates++;
      return;
    }
    _lastTimestamp = timestamp;
    if (cpu > 0)
      _cpu.Record(cpu);
    if (mainThread > 0 && !double.IsInfinity(mainThread))
    {
      _mainThread.Record(mainThread);
      _presentWait.Record(presentWait);
    }
    if (renderThread > 0)
      _renderThread.Record(renderThread);
    if (gpu > 0)
      _gpu.Record(gpu);
  }

  internal object DiagnosticState() =>
    new
    {
      unit = "ms",
      duplicateOrOlderTimings = _duplicates,
      missingTimestamps = _missingTimestamps,
      cpuFrame = _cpu.DiagnosticState(),
      cpuMainThread = _mainThread.DiagnosticState(),
      cpuRenderThread = _renderThread.DiagnosticState(),
      cpuPresentWait = _presentWait.DiagnosticState(),
      gpuFrame = _gpu.DiagnosticState(),
    };

  internal void ResetWindow()
  {
    _cpu = _mainThread = _renderThread = _presentWait = _gpu = default;
    _duplicates = _missingTimestamps = 0;
  }
}
