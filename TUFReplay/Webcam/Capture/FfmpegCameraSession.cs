using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Timing;

namespace TUFReplay.Webcam.Capture;

// One DirectShow process stays open from camera enable until camera disable. The
// native relay drains its frames and timestamps even when neither a run nor the
// live overlay is active; raw frames never enter managed memory.
internal sealed class FfmpegCameraSession : ICameraRelaySource, IDisposable
{
  private const int FramePoolCapacity = 8;
  private const int TimestampTimeoutMs = 10000;
  private readonly Process _process;
  private readonly WebcamCaptureProfile _profile;
  private readonly double _modeFrameRate;
  private readonly object _gate = new object();
  private IntPtr _camera;
  private int _nativeCalls;
  private bool _destroyRequested;
  private FfmpegCameraRecording _recording;
  private WebcamCaptureProfile _recordingProfile;
  private Task _shutdown = Task.CompletedTask;
  private volatile string _processError;
  private volatile bool _disposed;

  public CameraPreviewBuffer Preview { get; }
  public CameraRelayLibrary Relay { get; }
  public WebcamCaptureProfile RecordingProfile => _recordingProfile;
  public bool IsDisposed => _disposed;

  public CameraFrameSize SourceSize
  {
    get
    {
      CameraRelayState state = ReadState();
      return new CameraFrameSize(state.Width, state.Height);
    }
  }

  public double InputFrameRate
  {
    get
    {
      CameraRelayState state = ReadState();
      return state.InputRateNumerator > 0 && state.InputRateDenominator > 0
        ? (double)state.InputRateNumerator / state.InputRateDenominator
        : _modeFrameRate;
    }
  }

  public bool IsReady => ReadState().Ready != 0;

  public string Error => _processError ?? Describe((CameraRelayError)ReadState().Error);

  public string Diagnostic
  {
    get
    {
      if (!TryEnterNative(out IntPtr camera))
        return string.Empty;
      try
      {
        return Relay.GetDiagnostics(camera);
      }
      finally
      {
        ExitNative();
      }
    }
  }

  public FfmpegCameraSession(
    string executable,
    string deviceId,
    WebcamCaptureProfile profile,
    FfmpegCameraMode mode = null,
    CameraRelayLibrary relay = null
  )
  {
    _profile = profile;
    _modeFrameRate = mode?.FrameRate ?? 0;
    Relay = relay ?? CameraRelayLibrary.Load();
    Preview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    var captureBounds = new CameraFrameSize(profile.Width, profile.Height);
    var arguments = new List<string>
    {
      "-hide_banner",
      "-nostats",
      "-nostdin",
      "-filter_threads",
      "2",
      "-copyts",
      "-use_wallclock_as_timestamps",
      "1",
      "-f",
      "dshow",
      "-rtbufsize",
      "32M",
      "-threads",
      "2",
    };
    if (mode != null)
      arguments.AddRange(mode.InputArguments());
    string limiter =
      mode == null || mode.FrameRate > profile.FrameRate + 0.01
        ? "select='isnan(prev_selected_t)+gte(t-prev_selected_t,"
          + (0.9 / profile.FrameRate).ToString("R", CultureInfo.InvariantCulture)
          + ")',"
        : "";
    arguments.AddRange(
      new[]
      {
        "-i",
        "video=" + deviceId,
        "-an",
        "-vf",
        limiter
          + "scale=w='min("
          + captureBounds.Width
          + ",iw)':h='min("
          + captureBounds.Height
          + ",ih)':force_original_aspect_ratio=decrease:force_divisible_by=2:flags=bilinear,setsar=1,format=yuv420p,showinfo=checksum=0",
        "-threads",
        "2",
        "-c:v",
        "rawvideo",
        "-fps_mode",
        "passthrough",
        "-f",
        "rawvideo",
        "pipe:1",
      }
    );
    WebcamWallClock clock = WebcamWallClock.Capture();
    _process = FfmpegCameraProcess.Create(executable, arguments);
    _process.EnableRaisingEvents = true;
    _process.Exited += (_, _) =>
    {
      if (!_disposed)
        _processError = "The camera stopped sending video. Reconnect it or choose another camera.";
    };
    try
    {
      if (!_process.Start())
        throw new IOException("FFmpeg could not open the camera. Check its installation.");
      FfmpegCameraProcess.PreferGameplay(_process);
      _camera = Relay.Create(
        new CameraRelayConfig
        {
          FrameHandle = CameraRelayLibrary.OsHandle(_process.StandardOutput.BaseStream),
          LogHandle = CameraRelayLibrary.OsHandle(_process.StandardError.BaseStream),
          MaxWidth = captureBounds.Width,
          MaxHeight = captureBounds.Height,
          ClockUnixUs = clock.UnixUs,
          ClockTicks = clock.Timestamp,
          TicksFrequency = Stopwatch.Frequency,
          PoolCapacity = FramePoolCapacity,
          TimestampTimeoutMs = TimestampTimeoutMs,
          PreviewMaxWidth = Preview.Width,
          PreviewMaxHeight = Preview.Height,
        },
        Preview.Path,
        out string relayFailure
      );
      if (_camera == IntPtr.Zero)
        throw new IOException(
          "TUFReplay could not read the camera's video. Reinstall TUFReplay and try again. (" + relayFailure + ")"
        );
    }
    catch
    {
      try
      {
        if (!_process.HasExited)
          _process.Kill();
      }
      catch (InvalidOperationException) { }
      _process.Dispose();
      Preview.Dispose();
      throw;
    }
  }

  public async Task WaitUntilReadyAsync(int timeoutMilliseconds = 10000)
  {
    if (!TryEnterNative(out IntPtr camera))
      throw new IOException("Camera capture was stopped.");
    int result;
    try
    {
      result = await Task.Run(() => Relay.WaitReady(camera, timeoutMilliseconds)).ConfigureAwait(false);
    }
    finally
    {
      ExitNative();
    }
    if (result == 0)
      throw new IOException("The camera is not sending video. Reconnect it or choose another camera.");
    if (result < 0)
      throw new IOException(Error ?? "Camera capture was stopped.");
    lock (_gate)
    {
      if (_recordingProfile == null)
      {
        CameraFrameSize size = SourceSize;
        double inputRate = InputFrameRate;
        WebcamCaptureProfile recordingProfile = _profile;
        if (inputRate > 0)
          recordingProfile = recordingProfile.LimitFrameRate((int)Math.Round(Math.Min(_profile.FrameRate, inputRate)));
        _recordingProfile = recordingProfile.FitToSource(size.Width, size.Height);
      }
    }
  }

  public void SetRecording(FfmpegCameraRecording recording)
  {
    FfmpegCameraRecording previous;
    lock (_gate)
    {
      previous = _recording;
      _recording = recording;
    }
    // Ending a run seals its boundary at once; the writer finishes in the background.
    if (previous != null && previous != recording)
      previous.Seal();
    recording?.Connect(this);
  }

  // The relay reuses the latest frame when it is at most 250 ms old and not
  // earlier than the boundary, keeping its real capture timestamp.
  public IntPtr AttachRecording(long boundaryTicks, int frameRate, int queueCapacity)
  {
    if (!TryEnterNative(out IntPtr camera))
      return IntPtr.Zero;
    try
    {
      return Relay.Attach(camera, boundaryTicks, Stopwatch.GetTimestamp(), frameRate, queueCapacity);
    }
    finally
    {
      ExitNative();
    }
  }

  private CameraRelayState ReadState()
  {
    if (!TryEnterNative(out IntPtr camera))
      return new CameraRelayState { Error = (int)CameraRelayError.Stopped };
    try
    {
      return Relay.GetState(camera);
    }
    finally
    {
      ExitNative();
    }
  }

  // Native calls run under a lease, so the relay is never freed during a call.
  private bool TryEnterNative(out IntPtr camera)
  {
    lock (_gate)
    {
      camera = _camera;
      if (camera == IntPtr.Zero || _destroyRequested)
        return false;
      _nativeCalls++;
      return true;
    }
  }

  private void ExitNative()
  {
    lock (_gate)
    {
      _nativeCalls--;
      Monitor.PulseAll(_gate);
    }
  }

  private static string Describe(CameraRelayError error)
  {
    switch (error)
    {
      case CameraRelayError.None:
      case CameraRelayError.Stopped:
        return null;
      case CameraRelayError.ResolutionUnavailable:
        return "The camera resolution is unavailable. Reconnect it or choose another camera.";
      case CameraRelayError.ResolutionChanged:
        return "The camera changed resolution. Reconnect it before the next run.";
      case CameraRelayError.FellBehind:
        return "Camera frames fell behind. Choose a lower recording quality and reconnect the camera.";
      case CameraRelayError.OutOfMemory:
        return "There is not enough memory for camera capture. Choose a lower recording quality.";
      default:
        return "The camera stopped sending video. Reconnect it or choose another camera.";
    }
  }

  public void Dispose()
  {
    IntPtr camera;
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      camera = _camera;
      _recording = null;
    }
    // Stopping fails the attached run, releases frames and ends preview writes
    // before the shared mapping is disposed below.
    if (camera != IntPtr.Zero)
      Relay.Stop(camera);
    Task shutdown = Task.Run(() =>
    {
      try
      {
        if (!_process.HasExited)
          _process.Kill();
        if (!_process.WaitForExit(2000))
          throw new IOException("The camera did not disconnect. Reconnect it before trying again.");
      }
      finally
      {
        _process.Dispose();
        DestroyRelay();
      }
    });
    lock (_gate)
      _shutdown = shutdown;
    _ = shutdown.ContinueWith(
      completed =>
      {
        _ = completed.Exception;
      },
      TaskScheduler.Default
    );
    Preview.Dispose();
  }

  private void DestroyRelay()
  {
    IntPtr camera;
    lock (_gate)
    {
      _destroyRequested = true;
      while (_nativeCalls > 0)
        Monitor.Wait(_gate);
      camera = _camera;
      _camera = IntPtr.Zero;
    }
    // Reader threads end with the process; a stuck reader keeps its own reference.
    if (camera != IntPtr.Zero)
      Relay.Destroy(camera, 2000);
  }

  public Task StopAsync()
  {
    Dispose();
    lock (_gate)
      return _shutdown;
  }
}
