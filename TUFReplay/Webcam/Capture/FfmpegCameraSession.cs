using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Timing;

namespace TUFReplay.Webcam.Capture;

// One DirectShow process stays open from camera enable until camera disable.
// Its output is drained even when neither a run nor the live overlay is active.
internal sealed class FfmpegCameraSession : IDisposable
{
  private readonly struct FrameInfo
  {
    public FrameInfo(long ticks, int width, int height)
    {
      Ticks = ticks;
      Size = new CameraFrameSize(width, height);
    }

    public long Ticks { get; }
    public CameraFrameSize Size { get; }
  }

  private static readonly Regex TimeBase = new Regex(@"config in time_base: (\d+)/(\d+)", RegexOptions.Compiled);
  private static readonly Regex FrameRate = new Regex(@"\bframe_rate:\s*(\d+)/(\d+)(?:\s|$)", RegexOptions.Compiled);
  private static readonly Regex Timestamp = new Regex(@"\bn:\s*\d+\s+pts:\s*(-?\d+)", RegexOptions.Compiled);
  private static readonly Regex Dimensions = new Regex(@"\bs:(\d+)x(\d+)", RegexOptions.Compiled);
  private readonly Process _process;
  private readonly WebcamCaptureProfile _profile;
  private readonly CameraFrameSize _captureBounds;
  private readonly BlockingCollection<FrameInfo> _timestamps = new BlockingCollection<FrameInfo>(128);
  private readonly WebcamWallClock _clock = WebcamWallClock.Capture();
  private readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously
  );
  private readonly ConcurrentQueue<string> _messages = new ConcurrentQueue<string>();
  private CameraPreviewWorker _previewWorker;
  private volatile WebcamCaptureProfile _recordingProfile;
  private readonly object _sinkGate = new object();
  private Task _shutdown = Task.CompletedTask;
  private readonly Task _reader;
  private FfmpegCameraRecording _sink;
  private CameraFrame _latestFrame;
  private int _numerator;
  private int _denominator;
  private volatile bool _disposed;
  public CameraPreviewBuffer Preview { get; }
  public WebcamCaptureProfile RecordingProfile => _recordingProfile;
  public CameraFrameSize SourceSize { get; private set; }
  public double InputFrameRate { get; private set; }
  public string Error { get; private set; }
  public bool IsDisposed => _disposed;
  public bool IsReady => _ready.Task.Status == TaskStatus.RanToCompletion;
  public string Diagnostic => string.Join(" | ", _messages);

  public FfmpegCameraSession(
    string executable,
    string deviceId,
    WebcamCaptureProfile profile,
    FfmpegCameraMode mode = null
  )
  {
    _profile = profile;
    InputFrameRate = mode?.FrameRate ?? 0;
    Preview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    _captureBounds = new CameraFrameSize(profile.Width, profile.Height);
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
          + _captureBounds.Width
          + ",iw)':h='min("
          + _captureBounds.Height
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
    _process = FfmpegCameraProcess.Create(executable, arguments);
    _process.EnableRaisingEvents = true;
    _process.Exited += (_, _) =>
    {
      if (!_disposed)
      {
        Error = "The camera stopped sending video. Reconnect it or choose another camera.";
        _ready.TrySetException(new IOException(Error));
      }
    };
    _process.ErrorDataReceived += (_, args) => ParseTimestamp(args.Data);
    try
    {
      if (!_process.Start())
        throw new IOException("FFmpeg could not open the camera. Check its installation.");
      FfmpegCameraProcess.PreferGameplay(_process);
      _process.BeginErrorReadLine();
      _reader = Task.Factory.StartNew(
        ReadFrames,
        CancellationToken.None,
        TaskCreationOptions.LongRunning,
        TaskScheduler.Default
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
    if (await Task.WhenAny(_ready.Task, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false) != _ready.Task)
      throw new IOException("The camera is not sending video. Reconnect it or choose another camera.");
    await _ready.Task.ConfigureAwait(false);
  }

  public void SetRecording(FfmpegCameraRecording recording)
  {
    lock (_sinkGate)
    {
      _sink = recording;
      // The last complete frame remains owned by this gate while the reader
      // fills the other buffer. Keep its real capture timestamp when attaching.
      if (
        recording != null
        && _latestFrame != null
        && _latestFrame.IsFresh(Stopwatch.GetTimestamp(), Stopwatch.Frequency)
      )
        recording.Submit(_latestFrame);
    }
  }

  private void ParseTimestamp(string line)
  {
    if (line == null || _disposed)
      return;
    if (line.Contains("config in time_base:"))
    {
      Match timeBase = TimeBase.Match(line);
      if (timeBase.Success)
      {
        int.TryParse(timeBase.Groups[1].Value, out _numerator);
        int.TryParse(timeBase.Groups[2].Value, out _denominator);
      }
      if (TryParseInputFrameRate(line, out double frameRate))
        InputFrameRate = frameRate;
      return;
    }
    if (!line.Contains(" n:"))
    {
      if (!line.Contains("showinfo"))
      {
        _messages.Enqueue(line.Length > 500 ? line.Substring(0, 500) : line);
        while (_messages.Count > 16)
          _messages.TryDequeue(out _);
      }
      return;
    }
    Match timestamp = Timestamp.Match(line);
    Match dimensions = Dimensions.Match(line);
    if (
      timestamp.Success
      && dimensions.Success
      && _denominator > 0
      && long.TryParse(timestamp.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long pts)
    )
    {
      try
      {
        var frame = new FrameInfo(
          _clock.ToTimestamp(pts, _numerator, _denominator),
          int.Parse(dimensions.Groups[1].Value, CultureInfo.InvariantCulture),
          int.Parse(dimensions.Groups[2].Value, CultureInfo.InvariantCulture)
        );
        if (!_timestamps.TryAdd(frame))
          Error = "Camera frames fell behind. Choose a lower recording quality and reconnect the camera.";
      }
      catch (Exception exception) when (exception is InvalidOperationException || exception is OverflowException)
      {
        Error = exception.Message;
      }
    }
  }

  internal static bool TryParseInputFrameRate(string line, out double frameRate)
  {
    frameRate = 0;
    if (line == null || !line.Contains("config in time_base:"))
      return false;
    Match match = FrameRate.Match(line);
    if (
      !match.Success
      || !long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long numerator)
      || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long denominator)
      || numerator <= 0
      || denominator <= 0
    )
      return false;
    double value = (double)numerator / denominator;
    if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
      return false;
    frameRate = value;
    return true;
  }

  private void ReadFrames()
  {
    CameraFrame frame = null;
    try
    {
      CameraFramePool pool = null;
      Stream stream = _process.StandardOutput.BaseStream;
      while (!_disposed)
      {
        if (!_timestamps.TryTake(out FrameInfo info, 10000))
          throw new IOException("The camera stopped sending video. Reconnect it or choose another camera.");
        if (
          info.Size.Width < 2
          || info.Size.Height < 2
          || info.Size.Width > _captureBounds.Width
          || info.Size.Height > _captureBounds.Height
          || (info.Size.Width & 1) != 0
          || (info.Size.Height & 1) != 0
        )
          throw new IOException("The camera resolution is unavailable. Reconnect it or choose another camera.");
        if (frame == null)
        {
          SourceSize = info.Size;
          WebcamCaptureProfile recordingProfile = _profile;
          if (InputFrameRate > 0)
            recordingProfile = recordingProfile.LimitFrameRate(
              (int)Math.Round(Math.Min(_profile.FrameRate, InputFrameRate))
            );
          _recordingProfile = recordingProfile.FitToSource(info.Size.Width, info.Size.Height);
          pool = new CameraFramePool(info.Size.Width * info.Size.Height * 3 / 2);
          frame = pool.TryRent();
          lock (_sinkGate)
          {
            if (_disposed)
              return;
            _previewWorker = new CameraPreviewWorker(Preview, info.Size);
          }
        }
        else if (info.Size.Width != SourceSize.Width || info.Size.Height != SourceSize.Height)
          throw new IOException("The camera changed resolution. Reconnect it before the next run.");
        int read = 0;
        while (read < frame.Pixels.Length)
        {
          int count = stream.Read(frame.Pixels, read, frame.Pixels.Length - read);
          if (count == 0)
            throw new IOException("The camera stopped sending video. Reconnect it or choose another camera.");
          read += count;
        }
        if (Error != null)
          throw new IOException(Error);
        frame.TimestampTicks = info.Ticks;
        CameraFrame next = pool.TryRent();
        // Keep draining the device even if all consumers are temporarily busy.
        if (next == null)
          continue;
        lock (_sinkGate)
        {
          if (_disposed)
          {
            next.Release();
            return;
          }
          _sink?.Submit(frame);
          _previewWorker.Submit(frame);
          _latestFrame?.Release();
          _latestFrame = frame;
          frame = next;
          _ready.TrySetResult(true);
        }
      }
    }
    catch (Exception exception)
    {
      if (!_disposed)
      {
        Error = exception.Message;
        _ready.TrySetException(exception);
        lock (_sinkGate)
          _sink?.Fail(Error);
      }
    }
    finally
    {
      frame?.Release();
    }
  }

  public void Dispose()
  {
    lock (_sinkGate)
    {
      if (_disposed)
        return;
      _disposed = true;
      _ready.TrySetCanceled();
      _sink?.Fail("Camera capture was stopped.");
      _sink = null;
      _latestFrame?.Release();
      _latestFrame = null;
      _previewWorker?.Dispose();
      _timestamps.CompleteAdding();
      _shutdown = Task.Run(async () =>
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
          if (await Task.WhenAny(_reader, Task.Delay(2000)).ConfigureAwait(false) == _reader)
            _timestamps.Dispose();
          else
            _ = _reader.ContinueWith(
              completed =>
              {
                _ = completed.Exception;
                _timestamps.Dispose();
              },
              TaskScheduler.Default
            );
        }
      });
      _ = _shutdown.ContinueWith(
        completed =>
        {
          _ = completed.Exception;
        },
        TaskScheduler.Default
      );
    }
    Preview.Dispose();
  }

  public Task StopAsync()
  {
    Dispose();
    lock (_sinkGate)
      return _shutdown;
  }
}
