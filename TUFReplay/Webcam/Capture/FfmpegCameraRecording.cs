using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegCameraRecording
{
  // Unity/Mono's shared pool stops at 1 MiB. A 1080p YUV420 frame is
  // about 3 MiB, so give camera rentals an explicit, bounded 4 MiB bucket.
  private static readonly ArrayPool<byte> FrameBuffers = ArrayPool<byte>.Create(4 * 1024 * 1024, 5);

  private readonly struct Frame
  {
    public Frame(byte[] pixels, long ticks)
    {
      Pixels = pixels;
      Ticks = ticks;
    }

    public byte[] Pixels { get; }
    public long Ticks { get; }
  }

  private readonly Process _encoder;
  private readonly BlockingCollection<Frame> _frames = new BlockingCollection<Frame>(4);
  private readonly ConcurrentQueue<byte[]> _pool = new ConcurrentQueue<byte[]>();
  private readonly Task _writer;
  private readonly WebcamRecording _recording;
  private long _maxFrames;
  private readonly WebcamCaptureProfile _profile;
  private string _destination;
  private long _startTimestampTicks;
  private readonly int _frameByteCount;
  private readonly byte[][] _buffers = new byte[5][];
  private volatile string _error;
  private volatile bool _limited;
  private long _writtenFrames;
  private readonly ConcurrentQueue<string> _encoderMessages = new ConcurrentQueue<string>();
  private long _encodedDurationUs;

  public FfmpegCameraRecording(
    string executable,
    string runId,
    string path,
    string deviceId,
    WebcamCaptureProfile profile,
    CameraFrameSize sourceSize,
    long maxBytes
  )
  {
    _profile = profile;
    _recording = new WebcamRecording
    {
      RunId = runId,
      FilePath = path,
      DeviceId = deviceId,
      Width = profile.Width,
      Height = profile.Height,
      FrameRate = profile.FrameRate,
    };
    _maxFrames = Math.Max(1, (long)((maxBytes - 1024 * 1024) * 8d / (profile.BitRate * 1.5d) * profile.FrameRate));
    _frameByteCount = checked(sourceSize.Width * sourceSize.Height * 3 / 2);
    _encoder = FfmpegCameraProcess.Create(
      executable,
      new[]
      {
        "-hide_banner",
        "-nostats",
        "-nostdin",
        "-y",
        "-filter_threads",
        "2",
        "-f",
        "rawvideo",
        "-pixel_format",
        "yuv420p",
        "-video_size",
        sourceSize.Width + "x" + sourceSize.Height,
        "-framerate",
        profile.FrameRate.ToString(CultureInfo.InvariantCulture),
        "-i",
        "pipe:0",
        "-an",
        "-vf",
        "scale=" + profile.Width + ":" + profile.Height + ":flags=lanczos,setsar=1",
        "-c:v",
        "libx264",
        "-preset",
        "veryfast",
        "-threads",
        "2",
        "-pix_fmt",
        "yuv420p",
        "-b:v",
        profile.BitRate.ToString(CultureInfo.InvariantCulture),
        "-maxrate",
        profile.BitRate.ToString(CultureInfo.InvariantCulture),
        "-bufsize",
        (profile.BitRate * 2).ToString(CultureInfo.InvariantCulture),
        "-g",
        (profile.FrameRate * 2).ToString(CultureInfo.InvariantCulture),
        "-fs",
        Math.Max(1, maxBytes - 1024 * 1024).ToString(CultureInfo.InvariantCulture),
        "-movflags",
        "+faststart",
        "-progress",
        "pipe:1",
        "-f",
        "mp4",
        path,
      }
    );
    _encoder.ErrorDataReceived += (_, args) =>
    {
      if (args.Data != null)
      {
        _encoderMessages.Enqueue(args.Data);
        while (_encoderMessages.Count > 16)
          _encoderMessages.TryDequeue(out string ignored);
      }
      if (args.Data != null && args.Data.Contains("Error"))
        _error = "Camera video could not be encoded. Check FFmpeg and choose a lower recording quality.";
    };
    _encoder.OutputDataReceived += (_, args) =>
    {
      if (
        args.Data?.StartsWith("out_time_us=", StringComparison.Ordinal) == true
        && long.TryParse(args.Data.Substring(12), out long duration)
      )
        System.Threading.Interlocked.Exchange(ref _encodedDurationUs, duration);
    };
    try
    {
      if (!_encoder.Start())
        throw new IOException("The camera encoder could not start. Check FFmpeg.");
      _encoder.BeginErrorReadLine();
      _encoder.BeginOutputReadLine();
      for (int index = 0; index < _buffers.Length; index++)
      {
        _buffers[index] = FrameBuffers.Rent(_frameByteCount);
        _pool.Enqueue(_buffers[index]);
      }
      _writer = Task.Run(WriteFrames);
    }
    catch
    {
      _encoder.Dispose();
      ReleaseBuffers();
      _frames.Dispose();
      WebcamRecordingStore.Discard(_recording);
      throw;
    }
  }

  public void Attach(string runId, string destination, long maxBytes, long startTimestampTicks = 0)
  {
    if (_destination != null || maxBytes < 2 * 1024 * 1024)
      throw new InvalidOperationException("The camera recording could not be attached to this run.");
    _recording.RunId = runId;
    _destination = destination;
    _startTimestampTicks = startTimestampTicks;
    _maxFrames = Math.Max(1, (long)((maxBytes - 1024 * 1024) * 8d / (_profile.BitRate * 1.5d) * _profile.FrameRate));
  }

  public void Submit(byte[] source, long ticks)
  {
    if (ticks < _startTimestampTicks)
      return;
    if (_frames.IsAddingCompleted || _error != null || _limited)
      return;
    if (!_pool.TryDequeue(out byte[] pixels))
    {
      Fail("Camera encoding fell behind. Choose a lower recording quality for the next run.");
      return;
    }
    Buffer.BlockCopy(source, 0, pixels, 0, source.Length);
    try
    {
      if (_frames.TryAdd(new Frame(pixels, ticks)))
        return;
      _pool.Enqueue(pixels);
      Fail("Camera encoding fell behind. Choose a lower recording quality for the next run.");
    }
    catch (InvalidOperationException)
    {
      _pool.Enqueue(pixels);
    }
  }

  public void Fail(string message)
  {
    _error = message;
    _frames.CompleteAdding();
  }

  private void WriteFrames()
  {
    try
    {
      Stream output = _encoder.StandardInput.BaseStream;
      foreach (Frame frame in _frames.GetConsumingEnumerable())
      {
        try
        {
          if (_writtenFrames >= _maxFrames)
          {
            _limited = true;
            continue;
          }
          // Pool rentals may exceed the logical YUV frame size. Writing their
          // capacity would shift every following frame in the rawvideo stream.
          output.Write(frame.Pixels, 0, _frameByteCount);
          if (_writtenFrames == 0)
            _recording.CaptureStartTimestampTicks = frame.Ticks;
          _writtenFrames++;
        }
        finally
        {
          _pool.Enqueue(frame.Pixels);
        }
      }
      output.Close();
    }
    catch (Exception exception)
    {
      if (_encoder.HasExited && _encoder.ExitCode == 0 && _writtenFrames > 0)
        _limited = true;
      else
        _error = exception.Message;
      _frames.CompleteAdding();
    }
  }

  public async Task<WebcamRecording> FinishAsync()
  {
    _frames.CompleteAdding();
    try
    {
      if (await Task.WhenAny(_writer, Task.Delay(20000)) != _writer)
      {
        if (!_encoder.HasExited)
          _encoder.Kill();
        throw new IOException("The camera video could not be saved. Choose a lower recording quality and try again.");
      }
      await _writer;
      await Task.Run(() =>
      {
        if (!_encoder.WaitForExit(20000))
        {
          _encoder.Kill();
          throw new IOException("The camera encoder did not finish. Check FFmpeg.");
        }
        _encoder.WaitForExit();
      });
      if (_writtenFrames == 0 && _error == null)
      {
        TUFReplay.Shared.Capture.CaptureDiagnostics.Record(
          "recording.empty",
          new { _recording.RunId, exitCode = _encoder.ExitCode }
        );
        WebcamRecordingStore.Discard(_recording);
        return null;
      }
      if (_error != null || _encoder.ExitCode != 0)
      {
        TUFReplay.Shared.Capture.CaptureDiagnostics.Record(
          "recording.encoder.failed",
          new
          {
            _recording.RunId,
            writtenFrames = _writtenFrames,
            exitCode = _encoder.ExitCode,
            error = _error,
            stderr = _encoderMessages.ToArray(),
          }
        );
        throw new IOException(
          _error
            ?? "This camera video could not be saved. Check available disk space and try a lower recording quality."
        );
      }
      _recording.DurationUs =
        _encodedDurationUs > 0 ? _encodedDurationUs : _writtenFrames * 1_000_000 / _recording.FrameRate;
      _recording.SizeLimited = _limited;
      if (_destination != null)
      {
        File.Move(_recording.FilePath, _destination);
        _recording.FilePath = _destination;
      }
      return _recording;
    }
    catch
    {
      WebcamRecordingStore.Discard(_recording);
      throw;
    }
    finally
    {
      _encoder.Dispose();
      if (_writer.IsCompleted)
        CleanupBuffers();
      else
        _ = _writer.ContinueWith(
          completed =>
          {
            _ = completed.Exception;
            CleanupBuffers();
          },
          TaskScheduler.Default
        );
    }
  }

  public async Task CancelAsync()
  {
    _frames.CompleteAdding();
    try
    {
      await Task.Run(() =>
      {
        if (!_encoder.HasExited)
          _encoder.Kill();
        _encoder.WaitForExit(2000);
      });
      await Task.WhenAny(_writer, Task.Delay(2000));
    }
    finally
    {
      _encoder.Dispose();
      WebcamRecordingStore.Discard(_recording);
      if (_writer.IsCompleted)
        CleanupBuffers();
      else
        _ = _writer.ContinueWith(
          completed =>
          {
            _ = completed.Exception;
            CleanupBuffers();
          },
          TaskScheduler.Default
        );
    }
  }

  private void CleanupBuffers()
  {
    while (_frames.TryTake(out _)) { }
    ReleaseBuffers();
    _frames.Dispose();
  }

  private void ReleaseBuffers()
  {
    while (_pool.TryDequeue(out _)) { }
    foreach (byte[] buffer in _buffers)
      if (buffer != null)
        FrameBuffers.Return(buffer);
  }
}
