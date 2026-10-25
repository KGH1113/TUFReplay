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

// One run's camera video. The native relay queues the run's frames and streams
// them with their capture times to this run's FFmpeg encoder; managed code only
// starts, finishes and classifies the encoder.
internal sealed class FfmpegCameraRecording
{
  private const int QueueCapacity = 3;
  private const int FinishTimeoutMs = 20000;
  private readonly object _encoderGate = new object();
  private Process _encoder;
  private volatile bool _encoderStarted;
  private bool _stopRequested;
  private readonly FfmpegCameraEncoder _encoderPolicy;
  private readonly List<string> _arguments;
  private readonly WebcamRecording _recording;
  private readonly WebcamCaptureProfile _profile;
  private readonly string _executable;
  private readonly Action<string> _log;
  private long _maxBytes;
  private string _destination;
  private long _startTimestampTicks;
  private ICameraRelaySource _source;
  private CameraRelayLibrary _relay;
  private IntPtr _native;
  private Task _encoderStart = Task.CompletedTask;
  private volatile string _error;
  private string _pipeFailure;
  private volatile bool _limited;
  private volatile bool _captureFailed;
  private volatile bool _encoderFailed;
  private volatile bool _cancelRequested;
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

  // Called when the run starts. A prepared recording holds no encoder session;
  // the encoder starts now that this run's file-size limit is known.
  internal void Connect(ICameraRelaySource source)
  {
    if (_native != IntPtr.Zero)
      throw new InvalidOperationException("The camera recording is already connected.");
    IntPtr native = source.AttachRecording(_startTimestampTicks, _profile.FrameRate, QueueCapacity);
    if (native == IntPtr.Zero)
      throw new IOException(source.Error ?? "Camera capture was stopped before recording could start.");
    _source = source;
    _relay = source.Relay;
    _native = native;
    _encoderStart = Task.Run(StartEncoderAndConnect);
  }

  // The relay's progress for this run, for diagnostics and tests.
  internal CameraRelayRecordingStats RelayStats => _native == IntPtr.Zero ? default : _relay.GetStats(_native);

  // Ends the run boundary immediately. Frames already queued are still saved.
  internal void Seal()
  {
    if (_native != IntPtr.Zero)
      _relay.Seal(_native);
  }

  private void StartEncoderAndConnect()
  {
    try
    {
      Process encoder = StartEncoder();
      if (encoder == null)
        return;
      StreamWriter input = encoder.StandardInput;
      CameraRelayError error = _relay.SetOutput(_native, CameraRelayLibrary.OsHandle(input.BaseStream));
      // The relay writes through its own duplicate and closes it at the end of the
      // run, which lets FFmpeg finalize the MP4.
      input.Close();
      if (error != CameraRelayError.None)
        RecordEncoderFailure("The camera encoder input could not be connected.");
    }
    catch (Exception exception)
    {
      if (!_cancelRequested)
        RecordEncoderFailure(exception.Message);
    }
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
    try
    {
      if (_native == IntPtr.Zero)
      {
        // Prepared but never used by a run: no encoder ran and no file exists.
        WebcamRecordingStore.Discard(_recording);
        return null;
      }
      await _encoderStart.ConfigureAwait(false);
      CameraRelayRecordingStats stats = default;
      bool finished = await Task.Run(() => _relay.Finish(_native, FinishTimeoutMs, out stats)).ConfigureAwait(false);
      if (!finished)
      {
        RecordEncoderFailure("The encoder writer did not finish within 20 seconds.");
        throw new IOException("The camera video could not finish saving. Check disk space and reconnect the camera.");
      }
      if (stats.CaptureFailed != 0)
      {
        _captureFailed = true;
        if (_error == null)
          _error = _source?.Error ?? "Camera capture was stopped.";
      }
      if (stats.WriteFailed != 0)
        RecordEncoderFailure("The camera encoder stopped accepting video.");
      // -fs closes input before the MP4 finishes. Classify the closed pipe after
      // the encoder exits, not against a short deadline that could discard a
      // healthy large recording on a slower disk.
      if (stats.OutputClosedEarly != 0)
        _pipeFailure = "The camera encoder closed its input.";
      if (_error == null && stats.WrittenFrames == 0)
      {
        StopEncoder();
        WebcamRecordingStore.Discard(_recording);
        return null;
      }
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
      _recording.CaptureStartTimestampTicks = stats.FirstTimestampTicks;
      long encodedFrames = Interlocked.Read(ref _encodedFrames);
      _recording.SizeLimited =
        _limited
        || (encodedFrames > 0 && encodedFrames < stats.WrittenFrames)
        || new FileInfo(_recording.FilePath).Length >= Math.Max(1, _maxBytes - 1024 * 1024);
      // Progress includes packets the muxer may omit at -fs. Read the finalized
      // video track for a cutoff; normal capture timing stays exact.
      _recording.DurationUs = _recording.SizeLimited ? Mp4CameraDuration.ReadUs(_recording.FilePath) : stats.DurationUs;
      if (_destination != null)
      {
        File.Move(_recording.FilePath, _destination);
        _recording.FilePath = _destination;
      }
      _log?.Invoke(
        "Camera recording saved: encoded="
          + encodedFrames
          + ", dropped="
          + stats.DroppedFrames
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
    if (_native != IntPtr.Zero)
      _relay.Cancel(_native);
    try
    {
      // Killing the encoder unblocks a relay write that FFmpeg stopped reading.
      await Task.Run(StopEncoder).ConfigureAwait(false);
      await Task.WhenAny(_encoderStart, Task.Delay(2000)).ConfigureAwait(false);
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
    _ = Task.Run(async () =>
    {
      await Task.WhenAny(_encoderStart, Task.Delay(FinishTimeoutMs)).ConfigureAwait(false);
      IntPtr native = _native;
      _native = IntPtr.Zero;
      // A writer still blocked on a stalled encoder keeps its own reference.
      if (native != IntPtr.Zero)
        _relay.DestroyRecording(native, 2000);
      lock (_encoderGate)
        _encoder?.Dispose();
    });
  }
}
