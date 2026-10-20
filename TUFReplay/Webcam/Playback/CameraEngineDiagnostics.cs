using System;
using System.Diagnostics;
using TUFReplay.Webcam.Diagnostics;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine;

namespace TUFReplay.Webcam.Playback;

// Whole-player measurements, including time outside the camera's Update.
// Never enable the global profiler or deep profiling for these diagnostics.
internal sealed class CameraEngineDiagnostics : IDisposable
{
  private sealed class Metric : IDisposable
  {
    private const int Capacity = 8;
    private readonly string _name;
    private readonly ProfilerMarkerDataUnit _expectedUnit;
    private ProfilerRecorder _recorder;
    private CameraMetricStatistics _statistics;
    private string _unavailableReason = "marker_unavailable";
    private bool _failed;
    private long _overflowPolls;

    internal Metric(string name, ProfilerMarkerDataUnit unit = ProfilerMarkerDataUnit.TimeNanoseconds)
    {
      _name = name;
      _expectedUnit = unit;
    }

    internal void TryStart()
    {
      if (_recorder.Valid || _failed)
        return;
      try
      {
        _recorder = new ProfilerRecorder(
          _name,
          Capacity,
          ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately
        );
        if (!_recorder.Valid)
        {
          _recorder.Dispose();
          return;
        }
        if (_recorder.UnitType != _expectedUnit || _recorder.DataType != ProfilerMarkerDataType.Int64)
        {
          _unavailableReason = "unsupported_unit_or_data_type";
          _failed = true;
          _recorder.Dispose();
          return;
        }
        _unavailableReason = null;
      }
      catch (Exception exception)
      {
        Fail(exception);
      }
    }

    internal unsafe void Poll()
    {
      if (!_recorder.Valid)
        return;
      try
      {
        if (_recorder.WrappedAround)
          _overflowPolls++;
        // Drain completed samples atomically. LastValue alone can repeat a
        // stale value when a conditional marker (such as Canvas) stops firing.
        ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[Capacity];
        int count = _recorder.CopyTo(samples, Capacity, reset: true);
        double scale = _expectedUnit == ProfilerMarkerDataUnit.TimeNanoseconds ? 0.000001 : 1;
        for (int index = 0; index < count; index++)
          _statistics.Record(samples[index].Value * scale);
      }
      catch (Exception exception)
      {
        Fail(exception);
      }
    }

    private void Fail(Exception exception)
    {
      _failed = true;
      _unavailableReason = exception.GetType().Name + ": " + exception.Message;
      Dispose();
    }

    internal object SnapshotAndReset()
    {
      var snapshot = new
      {
        marker = _name,
        available = _recorder.Valid,
        unavailableReason = _unavailableReason,
        status = !_recorder.Valid ? "unavailable"
        : _statistics.Count == 0 ? "no_samples"
        : "measured",
        unit = _expectedUnit == ProfilerMarkerDataUnit.TimeNanoseconds ? "ms" : _expectedUnit.ToString(),
        overflowPolls = _overflowPolls,
        values = _statistics.DiagnosticState(),
      };
      _statistics = default;
      _overflowPolls = 0;
      return snapshot;
    }

    public void Dispose()
    {
      _recorder.Dispose();
    }
  }

  private readonly Metric[] _metrics =
  {
    new Metric("Main Thread"),
    new Metric("Render Thread"),
    new Metric("Gfx.WaitForPresentOnGfxThread"),
    new Metric("Gfx.WaitForRenderThread"),
    new Metric("Gfx.WaitForCommands"),
    new Metric("Gfx.PresentFrame"),
    new Metric("WaitForTargetFPS"),
    new Metric("Canvas.SendWillRenderCanvases"),
    new Metric("Canvas.BuildBatch"),
    new Metric("Draw Calls Count", ProfilerMarkerDataUnit.Count),
    new Metric("Batches Count", ProfilerMarkerDataUnit.Count),
    new Metric("SetPass Calls Count", ProfilerMarkerDataUnit.Count),
    new Metric("GC Allocated In Frame", ProfilerMarkerDataUnit.Bytes),
  };
  private readonly FrameTiming[] _timings = new FrameTiming[1];
  private readonly CameraFrameTimingStatistics _frameStatistics = new CameraFrameTimingStatistics();
  private int _lastFrame = -1;
  private bool _frameTimingEnabled;
  private string _frameTimingError;
  private long _emptyTimingPolls;
  private long _nextAvailabilityCheck;
  private CameraMetricStatistics _samplingCpu;

  internal CameraEngineDiagnostics()
  {
    RefreshAvailability();
  }

  internal void Poll(int frameId)
  {
    if (_lastFrame == frameId)
      return;
    _lastFrame = frameId;
    long started = Stopwatch.GetTimestamp();
    try
    {
      if (started >= _nextAvailabilityCheck)
        RefreshAvailability();
      PollCore();
    }
    finally
    {
      _samplingCpu.Record((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency);
    }
  }

  private void PollCore()
  {
    for (int index = 0; index < _metrics.Length; index++)
      _metrics[index].Poll();
    if (!_frameTimingEnabled)
      return;
    try
    {
      FrameTimingManager.CaptureFrameTimings();
      if (FrameTimingManager.GetLatestTimings(1, _timings) == 0)
      {
        _emptyTimingPolls++;
        return;
      }
      FrameTiming timing = _timings[0];
      _frameStatistics.Record(
        timing.frameStartTimestamp,
        timing.cpuFrameTime,
        timing.cpuMainThreadFrameTime,
        timing.cpuRenderThreadFrameTime,
        timing.cpuMainThreadPresentWaitTime,
        timing.gpuFrameTime
      );
    }
    catch (Exception exception)
    {
      _frameTimingEnabled = false;
      _frameTimingError = exception.GetType().Name + ": " + exception.Message;
    }
  }

  // Some markers are created lazily after the first rendered Canvas. Retry
  // missing markers every five seconds, even when short runs keep splitting
  // the reporting window before it reaches five seconds.
  private void RefreshAvailability()
  {
    _nextAvailabilityCheck = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 5;
    for (int index = 0; index < _metrics.Length; index++)
      _metrics[index].TryStart();
    if (_frameTimingError != null)
      return;
    try
    {
      _frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
    }
    catch (Exception exception)
    {
      _frameTimingError = exception.GetType().Name + ": " + exception.Message;
    }
  }

  internal object SnapshotAndReset()
  {
    var metrics = new object[_metrics.Length];
    for (int index = 0; index < _metrics.Length; index++)
      metrics[index] = _metrics[index].SnapshotAndReset();
    var snapshot = new
    {
      scope = "whole_player_not_camera_only",
      alignment = "completed_engine_samples_may_lag_game_context",
      profilerSampleMode = "sum_all_threads_per_engine_frame",
      samplingCpu = new { unit = "ms", values = _samplingCpu.DiagnosticState() },
      profilerMetrics = metrics,
      frameTiming = new
      {
        enabled = _frameTimingEnabled,
        unavailableReason = _frameTimingError ?? (_frameTimingEnabled ? null : "disabled_in_player"),
        emptyPolls = _emptyTimingPolls,
        metrics = _frameStatistics.DiagnosticState(),
      },
    };
    _frameStatistics.ResetWindow();
    _emptyTimingPolls = 0;
    _samplingCpu = default;
    return snapshot;
  }

  public void Dispose()
  {
    for (int index = 0; index < _metrics.Length; index++)
      _metrics[index].Dispose();
  }
}
