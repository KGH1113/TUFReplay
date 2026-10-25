using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegWebcamCaptureBackend : IWebcamCaptureBackend
{
  private readonly string _executable;
  private FfmpegCameraSession _session;
  private FfmpegCameraRecording _recording;
  private FfmpegCameraEncoder _encoderOverride;
  private string _deviceId;
  private string _recordingDirectory;
  private Task<FfmpegCameraRecording> _standby;
  private long _readyEncoderBytes;
  private volatile bool _hardwareEncoderFailed;
  public CameraPreviewBuffer Preview => _session?.Preview;
  public string CaptureWarning { get; private set; }

  public FfmpegWebcamCaptureBackend(string managedExecutable)
  {
    _executable = managedExecutable;
  }

  public async Task<List<WebcamDevice>> ListDevicesAsync()
  {
    using var process = FfmpegCameraProcess.Create(
      _executable,
      new[] { "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy" }
    );
    var output = new List<string>();
    process.ErrorDataReceived += (_, args) =>
    {
      if (args.Data != null)
        lock (output)
          if (output.Count < 400)
            output.Add(args.Data);
    };
    if (!process.Start())
      throw new IOException(
        "TUFReplay's FFmpeg could not start. Retry installation in the game and check your security software."
      );
    process.BeginErrorReadLine();
    await Task.Run(() =>
    {
      if (!process.WaitForExit(10000))
      {
        process.Kill();
        throw new IOException("Camera discovery timed out. Reconnect the camera and try again.");
      }
      process.WaitForExit();
    });
    lock (output)
      return FfmpegCameraDevices.Parse(output);
  }

  public async Task ArmAsync(string deviceId, WebcamCaptureProfile profile, string recordingDirectory = null)
  {
    await DisarmAsync();
    List<WebcamDevice> devices = await ListDevicesAsync();
    WebcamDevice device =
      deviceId == null ? devices.FirstOrDefault() : devices.FirstOrDefault(item => item.Id == deviceId);
    if (device == null)
      throw new IOException("The selected camera is unavailable. Reconnect it or select another camera.");
    _deviceId = device.Id;
    _encoderOverride = null;
    _recordingDirectory = recordingDirectory ?? Path.GetTempPath();
    Directory.CreateDirectory(_recordingDirectory);
    IReadOnlyList<FfmpegCameraMode> modes = await FfmpegCameraModes.DiscoverAsync(_executable, _deviceId, profile, Log);
    // Try smooth supported modes, then the driver's default, before accepting a
    // slow raw fallback. Bound both the number of attempts and total wait time.
    var startup = Stopwatch.StartNew();
    Exception lastFailure = null;
    foreach (FfmpegCameraMode mode in FfmpegCameraModes.BuildAttempts(modes, profile))
    {
      int remainingMs = (int)Math.Max(0, 30000 - startup.ElapsedMilliseconds);
      if (remainingMs == 0)
        break;
      var session = new FfmpegCameraSession(_executable, _deviceId, profile, mode);
      try
      {
        await session.WaitUntilReadyAsync(Math.Min(10000, remainingMs));
        if (session.Error != null)
          throw new IOException(session.Error);
        _session = session;
        Log("Camera capture mode: " + (mode?.ToString() ?? "device default"));
        double inputRate = session.InputFrameRate;
        Log(
          "Camera output: "
            + session.SourceSize.Width
            + "x"
            + session.SourceSize.Height
            + "; input_fps="
            + inputRate.ToString("0.######", CultureInfo.InvariantCulture)
            + "; target_fps="
            + profile.FrameRate
        );
        if (inputRate > 0 && inputRate < profile.FrameRate * 0.995)
        {
          CaptureWarning =
            "Camera connected at "
            + inputRate.ToString("0.##", CultureInfo.InvariantCulture)
            + " fps; this quality targets "
            + profile.FrameRate
            + " fps. Try a lower camera quality and reconnect.";
          Log("Camera frame rate reduced: " + CaptureWarning);
        }
        break;
      }
      catch (Exception exception)
      {
        lastFailure = exception;
        Log("Camera mode failed: " + (mode?.ToString() ?? "device default") + " | " + session.Diagnostic);
        await session.StopAsync();
      }
    }
    if (_session == null)
      throw new IOException(
        "The camera could not start with its supported settings. Close other apps using the camera, reconnect it, and try again.",
        lastFailure
      );
    try
    {
      PrepareNextRecording();
      await _standby;
    }
    catch
    {
      await DisarmAsync();
      throw;
    }
  }

  private void PrepareNextRecording()
  {
    if (_standby != null || _session == null || _session.IsDisposed)
      return;
    FfmpegCameraSession session = _session;
    string device = _deviceId;
    string directory = _recordingDirectory;
    FfmpegCameraEncoder encoderOverride = _encoderOverride;
    long readyBytes = _readyEncoderBytes;
    bool recording = _recording != null;
    _standby = Task.Run(async () =>
    {
      await session.WaitUntilReadyAsync();
      FfmpegCameraEncoder encoder =
        encoderOverride ?? await FfmpegCameraEncoder.SelectAsync(_executable, session.RecordingProfile, Log);
      if (session.IsDisposed || session.Error != null)
        throw new OperationCanceledException();
      Log("Camera encoder selected: " + encoder.Name);
      FfmpegCameraRecording prepared = CreateRecording(session, device, directory, encoder);
      return readyBytes > 0 ? await WarmAsync(prepared, session, readyBytes, recording) : prepared;
    });
  }

  private FfmpegCameraRecording CreateRecording(
    FfmpegCameraSession session,
    string device,
    string directory,
    FfmpegCameraEncoder encoder
  ) =>
    new FfmpegCameraRecording(
      _executable,
      null,
      Path.Combine(directory, Guid.NewGuid().ToString("N") + ".mp4.partial"),
      device,
      session.RecordingProfile,
      session.SourceSize,
      WebcamRecordingStore.MaximumCaptureBytes,
      encoder,
      Log
    );

  private static async Task<FfmpegCameraRecording> WarmAsync(
    FfmpegCameraRecording recording,
    FfmpegCameraSession session,
    long maxBytes,
    bool yieldToRecording
  )
  {
    try
    {
      if (session.IsDisposed || session.Error != null)
        throw new OperationCanceledException();
      recording.Warm(session, maxBytes, yieldToRecording);
      return recording;
    }
    catch
    {
      await recording.CancelAsync();
      throw;
    }
  }

  public async Task KeepEncoderReadyAsync(long maxBytes)
  {
    _readyEncoderBytes = maxBytes;
    await ApplySoftwareFallbackAsync();
    if (_session == null || _session.IsDisposed)
      return;
    if (_standby == null)
    {
      PrepareNextRecording();
      return;
    }
    FfmpegCameraSession session = _session;
    Task<FfmpegCameraRecording> standby = _standby;
    bool recording = _recording != null;
    _standby = Task.Run(async () =>
    {
      FfmpegCameraRecording prepared = await standby;
      return prepared.IsWarm ? prepared : await WarmAsync(prepared, session, maxBytes, recording);
    });
  }

  // Closes a warm encoder session when the level closes; the next run prepares
  // without holding one until a level opens again.
  public async Task ReleaseEncoderAsync()
  {
    _readyEncoderBytes = 0;
    Task<FfmpegCameraRecording> standby = _standby;
    if (standby == null)
      return;
    FfmpegCameraRecording recording;
    try
    {
      recording = await standby;
    }
    catch (Exception)
    {
      if (_standby == standby)
        _standby = null;
      PrepareNextRecording();
      return;
    }
    if (!recording.IsWarm || _standby != standby)
      return;
    _standby = null;
    await recording.CancelAsync();
    PrepareNextRecording();
  }

  public async Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0)
  {
    if (_recording != null || _session?.IsReady != true)
      throw new IOException("The camera is not sending video. Reconnect it before the next run.");
    if (_session.Error != null)
      throw new IOException(_session.Error);
    await ApplySoftwareFallbackAsync();
    // Warm the next run's encoder with this run's limit, which may have changed.
    if (_readyEncoderBytes > 0)
      _readyEncoderBytes = maxBytes;
    PrepareNextRecording();
    Task<FfmpegCameraRecording> prepared = _standby;
    _standby = null;
    FfmpegCameraSession session = _session;
    FfmpegCameraRecording recording = await prepared;
    try
    {
      if (session != _session || session.IsDisposed || session.Error != null)
        throw new IOException(session.Error ?? "Camera capture was stopped before recording could start.");
      if (!recording.CanRecord(maxBytes, out string reason))
      {
        Log("Camera encoder restarted for this run: " + reason);
        await recording.CancelAsync();
        recording = CreateRecording(session, _deviceId, _recordingDirectory, recording.Encoder);
      }
      recording.Attach(runId, path, maxBytes, startTimestampTicks);
      session.SetRecording(recording);
      _recording = recording;
    }
    catch
    {
      await recording.CancelAsync();
      throw;
    }
    // This run's video continues through the fail screen until a retry starts.
    // Warm the next run's encoder now, so a retry does not wait for it.
    PrepareNextRecording();
  }

  // Seals the run at once. The returned task finishes saving its video and may
  // still run while the next run begins.
  public Task<WebcamRecording> EndAsync() => EndAsync(true);

  private async Task<WebcamRecording> EndAsync(bool prepareNext)
  {
    FfmpegCameraRecording recording = _recording;
    _recording = null;
    _session?.SetRecording(null);
    if (prepareNext)
      PrepareNextRecording();
    if (recording == null)
      return null;
    try
    {
      return await recording.FinishAsync();
    }
    catch
    {
      // A driver/session can become unavailable after the startup probe. Keep
      // healthy capture open and avoid repeatedly choosing that GPU this session.
      // Saving can overlap the next run, so the next camera operation switches.
      if (recording.HardwareEncoderFailed)
        _hardwareEncoderFailed = true;
      throw;
    }
  }

  private async Task ApplySoftwareFallbackAsync()
  {
    if (!_hardwareEncoderFailed)
      return;
    _hardwareEncoderFailed = false;
    _encoderOverride = FfmpegCameraEncoder.Software;
    Task<FfmpegCameraRecording> standby = _standby;
    _standby = null;
    await DiscardStandby(standby);
  }

  public async Task DisarmAsync()
  {
    CaptureWarning = null;
    try
    {
      WebcamRecordingStore.Discard(await EndAsync(false));
    }
    finally
    {
      FfmpegCameraSession session = _session;
      _session = null;
      Task<FfmpegCameraRecording> standby = _standby;
      _standby = null;
      try
      {
        if (session != null)
          await session.StopAsync();
      }
      finally
      {
        await DiscardStandby(standby);
      }
    }
  }

  private static void Log(string message) => UnityMainThread.Post(() => Main.Instance?.Log("[Webcam] " + message));

  public void Dispose()
  {
    _session?.Dispose();
    _session = null;
    Task<FfmpegCameraRecording> standby = _standby;
    _standby = null;
    _ = DiscardStandby(standby);
    FfmpegCameraRecording recording = _recording;
    _recording = null;
    if (recording != null)
      _ = recording.CancelAsync();
  }

  private static async Task DiscardStandby(Task<FfmpegCameraRecording> standby)
  {
    if (standby == null)
      return;
    try
    {
      await (await standby).CancelAsync();
    }
    catch (Exception) { }
  }
}
