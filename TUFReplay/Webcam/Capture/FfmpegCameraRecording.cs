using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegCameraRecording
{
  private readonly object _encoderGate = new object();
  private Process _encoder;
  private volatile bool _encoderStarted;
  private bool _stopRequested;
  private readonly FfmpegCameraEncoder _encoderPolicy;
  private readonly List<string> _arguments;
  private readonly CameraFrameQueue _frames = new CameraFrameQueue(3);
  private readonly Task _writer;
  private readonly WebcamRecording _recording;
  private readonly WebcamCaptureProfile _profile;
  private readonly CameraFrameSize _sourceSize;
  private readonly string _executable;
  private readonly Action<string> _log;
  private long _maxBytes;
  private string _destination;
  private long _startTimestampTicks;
  private long _lastSubmittedTicks = long.MinValue;
  private volatile string _error;
  private string _pipeFailure;
  private volatile bool _limited;
  private volatile bool _captureFailed;
  private volatile bool _encoderFailed;
  private volatile bool _cancelRequested;
  private long _writtenFrames;
  private long _durationUs;
  private long _encodedFrames;
  private int _cleanupScheduled;
  private readonly ConcurrentQueue<string> _encoderMessages = new ConcurrentQueue<string>();
  private readonly TaskCompletionSource<bool> _outputClosed = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously
  );
  private readonly TaskCompletionSource<bool> _errorClosed = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously
  );

  public bool HardwareEncoderFailed =>
    _encoderPolicy.IsHardware && _encoderFailed && !_captureFailed && !_cancelRequested;

  public FfmpegCameraRecording(
    string executable,
    string runId,
    string path,
    string deviceId,
    WebcamCaptureProfile profile,
    CameraFrameSize sourceSize,
    long maxBytes,
    FfmpegCameraEncoder encoder = null,
    Action<string> log = null
  )
  {
    _executable = executable;
    _profile = profile;
    _sourceSize = sourceSize;
    _log = log;
    _maxBytes = maxBytes;
    _encoderPolicy = encoder ?? FfmpegCameraEncoder.Software;
    _recording = new WebcamRecording
    {
      RunId = runId,
      FilePath = path,
      DeviceId = deviceId,
      Width = profile.Width,
      Height = profile.Height,
      FrameRate = profile.FrameRate,
    };
    _arguments = new List<string>
    {
      "-hide_banner",
      "-loglevel",
      "warning",
      "-nostats",
      "-nostdin",
      "-y",
      "-filter_threads",
      "1",
      "-probesize",
      "32",
      "-analyzeduration",
      "0",
      "-fpsprobesize",
      "0",
      "-f",
      "matroska",
      "-i",
      "pipe:0",
      "-an",
    };
    // Capture already scales to the recording bounds. Do not run a second
    // scaler in the normal path (including Compact with its smaller preview).
    if (sourceSize.Width != profile.Width || sourceSize.Height != profile.Height)
      _arguments.AddRange(
        new[] { "-vf", "scale=" + profile.Width + ":" + profile.Height + ":flags=bilinear,setsar=1" }
      );
    _arguments.AddRange(_encoderPolicy.GetArguments(profile));
    // Encoder selection was probed during preparation. FFmpeg cannot initialize
    // its real encoder before the first frame anyway, so start it on the writer
    // thread only after Attach supplies this run's exact file limit.
    _writer = Task.Factory.StartNew(
      WriteFrames,
      CancellationToken.None,
      TaskCreationOptions.LongRunning,
      TaskScheduler.Default
    );
  }

  public void Attach(string runId, string destination, long maxBytes, long startTimestampTicks = 0)
  {
    if (_destination != null || maxBytes < 2 * 1024 * 1024)
      throw new InvalidOperationException("The camera recording could not be attached to this run.");
    _recording.RunId = runId;
    _destination = destination;
    _startTimestampTicks = startTimestampTicks;
    _maxBytes = maxBytes;
  }

  // Called serially under the session's sink gate. Only retain the frame;
  // neither a multi-megabyte copy nor process startup runs on capture.
  public void Submit(CameraFrame frame)
  {
    if (
      frame.TimestampTicks < _startTimestampTicks
      || frame.TimestampTicks <= _lastSubmittedTicks
      || _error != null
      || _limited
      || _cancelRequested
    )
      return;
    if (_frames.Submit(frame))
      _lastSubmittedTicks = frame.TimestampTicks;
  }

  public void Fail(string message)
  {
    _captureFailed = true;
    _error = message;
    _frames.Complete();
  }

  private Process StartEncoder()
  {
    lock (_encoderGate)
    {
      if (_stopRequested || _cancelRequested || _captureFailed)
        return null;
      var arguments = new List<string>(_arguments);
      arguments.AddRange(
        new[]
        {
          "-fs",
          Math.Max(1, _maxBytes - 1024 * 1024).ToString(CultureInfo.InvariantCulture),
          "-movflags",
          "+faststart",
          "-progress",
          "pipe:1",
          "-f",
          "mp4",
          _recording.FilePath,
        }
      );
      _encoder = FfmpegCameraProcess.Create(_executable, arguments);
      _encoder.ErrorDataReceived += (_, args) =>
      {
        if (args.Data == null)
        {
          _errorClosed.TrySetResult(true);
          return;
        }
        _encoderMessages.Enqueue(args.Data.Length > 500 ? args.Data.Substring(0, 500) : args.Data);
        while (_encoderMessages.Count > 16)
          _encoderMessages.TryDequeue(out string ignored);
      };
      _encoder.OutputDataReceived += (_, args) =>
      {
        if (args.Data == null)
          _outputClosed.TrySetResult(true);
        else if (
          args.Data.StartsWith("frame=", StringComparison.Ordinal)
          && long.TryParse(args.Data.Substring(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out long frames)
        )
          Interlocked.Exchange(ref _encodedFrames, frames);
      };
      if (!_encoder.Start())
        throw new IOException("The camera encoder could not start. Retry FFmpeg installation in Downloads.");
      _encoderStarted = true;
      FfmpegCameraProcess.PreferGameplay(_encoder);
      _encoder.BeginErrorReadLine();
      _encoder.BeginOutputReadLine();
      return _encoder;
    }
  }

  private void WriteFrames()
  {
    CameraFrame frame = null;
    try
    {
      frame = _frames.Take();
      if (frame == null)
        return;
      Process encoder = StartEncoder();
      if (encoder == null)
        return;
      using Stream output = encoder.StandardInput.BaseStream;
      var stream = new FfmpegCameraFrameStream(
        output,
        _sourceSize.Width,
        _sourceSize.Height,
        _profile.FrameRate,
        Stopwatch.Frequency
      );
      do
      {
        stream.WriteFrame(frame.Pixels, frame.TimestampTicks);
        if (_writtenFrames == 0)
          _recording.CaptureStartTimestampTicks = stream.FirstTimestampTicks;
        _durationUs = stream.DurationUs;
        _writtenFrames++;
        frame.Release();
        frame = null;
      } while ((frame = _frames.Take()) != null);
    }
    catch (Exception exception)
    {
      if (!_cancelRequested)
      {
        // -fs closes input before +faststart finishes moving MP4 metadata.
        // Classify a broken pipe after final exit, not against a short deadline
        // that can discard a healthy large recording on a slower disk.
        if (_writtenFrames > 0 && exception is IOException && _encoderStarted)
          _pipeFailure = exception.Message;
        else
          RecordEncoderFailure(exception.Message);
      }
    }
    finally
    {
      frame?.Release();
      _frames.Dispose();
    }
  }

  private void RecordEncoderFailure(string message)
  {
    if (_error == null)
      _error = message;
    if (_encoderStarted && !_captureFailed && !_cancelRequested)
      _encoderFailed = true;
  }

  private async Task WaitForOutputAsync()
  {
    if (!_encoderStarted)
      return;
    await Task.WhenAny(Task.WhenAll(_outputClosed.Task, _errorClosed.Task), Task.Delay(1000)).ConfigureAwait(false);
  }

  private bool HasOutputFailure()
  {
    foreach (string message in _encoderMessages)
      if (
        message.IndexOf("No space left", StringComparison.OrdinalIgnoreCase) >= 0
        || message.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0
        || message.IndexOf("Read-only file system", StringComparison.OrdinalIgnoreCase) >= 0
      )
        return true;
    return false;
  }

  public async Task<WebcamRecording> FinishAsync()
  {
    _frames.Complete();
    try
    {
      if (await Task.WhenAny(_writer, Task.Delay(20000)).ConfigureAwait(false) != _writer)
      {
        RecordEncoderFailure("The encoder writer did not finish within 20 seconds.");
        throw new IOException("The camera video could not finish saving. Check disk space and reconnect the camera.");
      }
      await _writer.ConfigureAwait(false);
      if (_encoderStarted)
      {
        if (!await Task.Run(() => _encoder.WaitForExit(20000)).ConfigureAwait(false))
        {
          RecordEncoderFailure("FFmpeg did not exit within 20 seconds.");
          throw new IOException("The camera encoder did not finish saving. Restart the camera and try again.");
        }
        await WaitForOutputAsync().ConfigureAwait(false);
        if (_encoder.ExitCode != 0)
          RecordEncoderFailure(_pipeFailure ?? "FFmpeg exited with code " + _encoder.ExitCode + ".");
        else if (_pipeFailure != null)
          _limited = true;
      }
      if (_error != null)
        throw new IOException(
          "The camera video could not be saved. Restart the camera and check disk space. Your input replay is still available."
        );
      if (_writtenFrames == 0)
      {
        WebcamRecordingStore.Discard(_recording);
        return null;
      }
      long encodedFrames = Interlocked.Read(ref _encodedFrames);
      _recording.SizeLimited =
        _limited
        || (encodedFrames > 0 && encodedFrames < _writtenFrames)
        || new FileInfo(_recording.FilePath).Length >= Math.Max(1, _maxBytes - 1024 * 1024);
      // Progress includes packets the muxer may omit at -fs. Read the finalized
      // video track for a cutoff; normal capture timing stays exact.
      _recording.DurationUs = _recording.SizeLimited ? Mp4CameraDuration.ReadUs(_recording.FilePath) : _durationUs;
      if (_destination != null)
      {
        File.Move(_recording.FilePath, _destination);
        _recording.FilePath = _destination;
      }
      _log?.Invoke(
        "Camera recording saved: encoded="
          + encodedFrames
          + ", dropped="
          + _frames.Dropped
          + ", durationUs="
          + _recording.DurationUs
      );
      return _recording;
    }
    catch (Exception exception)
    {
      StopEncoder();
      await WaitForOutputAsync().ConfigureAwait(false);
      if (HasOutputFailure())
        _encoderFailed = false;
      if (HardwareEncoderFailed)
        FfmpegCameraEncoder.Invalidate(_executable, _profile);
      _log?.Invoke(
        "Camera recording failed ("
          + _encoderPolicy.Name
          + "): "
          + (_error ?? exception.Message)
          + " | "
          + string.Join(" | ", _encoderMessages)
      );
      WebcamRecordingStore.Discard(_recording);
      throw;
    }
    finally
    {
      CleanupWhenFinished();
    }
  }

  public async Task CancelAsync()
  {
    _cancelRequested = true;
    _frames.Dispose();
    try
    {
      await Task.Run(StopEncoder).ConfigureAwait(false);
      await Task.WhenAny(_writer, Task.Delay(2000)).ConfigureAwait(false);
    }
    finally
    {
      WebcamRecordingStore.Discard(_recording);
      CleanupWhenFinished();
    }
  }

  private void StopEncoder()
  {
    lock (_encoderGate)
    {
      _stopRequested = true;
      if (_encoder == null || !_encoderStarted)
        return;
      try
      {
        if (!_encoder.HasExited)
          _encoder.Kill();
        _encoder.WaitForExit(2000);
      }
      catch (InvalidOperationException) { }
      catch (System.ComponentModel.Win32Exception) { }
    }
  }

  private void CleanupWhenFinished()
  {
    if (Interlocked.Exchange(ref _cleanupScheduled, 1) != 0)
      return;
    if (_writer.IsCompleted)
      Cleanup();
    else
      _ = _writer.ContinueWith(
        completed =>
        {
          _ = completed.Exception;
          Cleanup();
          WebcamRecordingStore.Discard(_recording);
        },
        TaskScheduler.Default
      );
  }

  private void Cleanup()
  {
    _frames.Dispose();
    lock (_encoderGate)
      _encoder?.Dispose();
  }
}
