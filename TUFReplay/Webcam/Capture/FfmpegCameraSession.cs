using System;
using System.Collections.Concurrent;
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
  private static readonly Regex Timestamp = new Regex(@"\bn:\s*\d+\s+pts:\s*(-?\d+)", RegexOptions.Compiled);
  private static readonly Regex Dimensions = new Regex(@"\bs:(\d+)x(\d+)", RegexOptions.Compiled);
  private readonly Process _process;
  private readonly WebcamCaptureProfile _profile;
  private readonly CameraFrameSize _captureBounds;
  private readonly BlockingCollection<FrameInfo> _timestamps = new BlockingCollection<FrameInfo>(128);
  private readonly WebcamWallClock _clock = WebcamWallClock.Capture();
  private byte[] _previewPixels;
  private CameraPreviewPixels _previewConverter;
  private volatile WebcamCaptureProfile _recordingProfile;
  private readonly object _sinkGate = new object();
  private FfmpegCameraRecording _sink;
  private int _numerator;
  private int _denominator;
  private volatile bool _disposed;
  public CameraPreviewBuffer Preview { get; }
  public WebcamCaptureProfile RecordingProfile => _recordingProfile;
  public CameraFrameSize SourceSize { get; private set; }
  public string Error { get; private set; }

  public FfmpegCameraSession(string executable, string deviceId, WebcamCaptureProfile profile)
  {
    _profile = profile;
    Preview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    _captureBounds = new CameraFrameSize(
      Math.Max(profile.Width, Preview.Width),
      Math.Max(profile.Height, Preview.Height)
    );
    _process = FfmpegCameraProcess.Create(
      executable,
      new[]
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
        "16M",
        "-framerate",
        profile.FrameRate.ToString(CultureInfo.InvariantCulture),
        "-i",
        "video=" + deviceId,
        "-an",
        "-vf",
        "scale=w='min("
          + _captureBounds.Width
          + ",iw)':h='min("
          + _captureBounds.Height
          + ",ih)':force_original_aspect_ratio=decrease:force_divisible_by=2:flags=lanczos,setsar=1,fps="
          + profile.FrameRate
          + ",format=yuv420p,showinfo=checksum=0",
        "-threads",
        "2",
        "-c:v",
        "rawvideo",
        "-f",
        "rawvideo",
        "pipe:1",
      }
    );
    _process.ErrorDataReceived += (_, args) => ParseTimestamp(args.Data);
    try
    {
      if (!_process.Start())
        throw new IOException("FFmpeg could not open the camera. Check its installation.");
      _process.BeginErrorReadLine();
      Task.Run(ReadFrames);
    }
    catch
    {
      _process.Dispose();
      Preview.Dispose();
      throw;
    }
  }

  public void SetRecording(FfmpegCameraRecording recording)
  {
    lock (_sinkGate)
      _sink = recording;
  }

  private void ParseTimestamp(string line)
  {
    if (line == null || _disposed)
      return;
    Match timeBase = TimeBase.Match(line);
    if (timeBase.Success)
    {
      int.TryParse(timeBase.Groups[1].Value, out _numerator);
      int.TryParse(timeBase.Groups[2].Value, out _denominator);
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
    else if (!line.Contains("showinfo") && line.Contains("Error"))
      Error = "The camera could not be read. Reconnect it or choose another camera.";
  }

  private void ReadFrames()
  {
    try
    {
      byte[] frame = null;
      CameraFrameSize previewSize = default;
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
          _recordingProfile = _profile.FitToSource(info.Size.Width, info.Size.Height);
          frame = new byte[info.Size.Width * info.Size.Height * 3 / 2];
          previewSize = CameraFrameSize.Fit(info.Size.Width, info.Size.Height, Preview.Width, Preview.Height);
          _previewPixels = new byte[previewSize.ByteCount];
          _previewConverter = new CameraPreviewPixels(
            info.Size.Width,
            info.Size.Height,
            previewSize.Width,
            previewSize.Height
          );
        }
        else if (info.Size.Width != SourceSize.Width || info.Size.Height != SourceSize.Height)
          throw new IOException("The camera changed resolution. Reconnect it before the next run.");
        int read = 0;
        while (read < frame.Length)
        {
          int count = stream.Read(frame, read, frame.Length - read);
          if (count == 0)
            throw new IOException("The camera stopped sending video. Reconnect it or choose another camera.");
          read += count;
        }
        if (Error != null)
          throw new IOException(Error);
        if (Preview.ShouldPublish)
        {
          _previewConverter.Convert(frame, _previewPixels);
          Preview.Publish(_previewPixels, previewSize.Width, previewSize.Height);
        }
        lock (_sinkGate)
          _sink?.Submit(frame, info.Ticks);
      }
    }
    catch (Exception exception)
    {
      if (!_disposed)
      {
        Error = exception.Message;
        lock (_sinkGate)
          _sink?.Fail(Error);
      }
    }
  }

  public void Dispose()
  {
    _disposed = true;
    lock (_sinkGate)
    {
      _sink?.Fail("Camera capture was stopped.");
      _sink = null;
    }
    try
    {
      if (!_process.HasExited)
        _process.Kill();
    }
    catch (InvalidOperationException) { }
    _process.Dispose();
    Preview.Dispose();
  }
}
