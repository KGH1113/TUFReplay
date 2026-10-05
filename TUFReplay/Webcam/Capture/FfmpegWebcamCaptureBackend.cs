using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TUFReplay.Shared.Capture;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Capture;

internal sealed class FfmpegWebcamCaptureBackend : IWebcamCaptureBackend
{
  private readonly string _executable;
  private FfmpegCameraSession _session;
  private FfmpegCameraRecording _recording;
  private WebcamCaptureProfile _profile;
  private string _deviceId;
  private string _recordingDirectory;
  private Task<FfmpegCameraRecording> _standby;
  public CameraPreviewBuffer Preview => _session?.Preview;

  public FfmpegWebcamCaptureBackend(string configuredPath, string payloadPath)
  {
    string bundled = Path.Combine(payloadPath, "Helpers", "win", "ffmpeg.exe");
    _executable =
      !string.IsNullOrWhiteSpace(configuredPath) ? configuredPath
      : File.Exists(bundled) ? bundled
      : "ffmpeg.exe";
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
      throw new IOException("FFmpeg could not start. Select an FFmpeg executable in camera settings.");
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
    _profile = profile;
    _recordingDirectory = recordingDirectory ?? Path.GetTempPath();
    Directory.CreateDirectory(_recordingDirectory);
    _session = new FfmpegCameraSession(_executable, _deviceId, profile);
    PrepareNextRecording();
    await _standby;
  }

  private void PrepareNextRecording()
  {
    if (_standby != null || _session == null || _session.IsDisposed)
      return;
    FfmpegCameraSession session = _session;
    string device = _deviceId;
    string directory = _recordingDirectory;
    _standby = Task.Run(async () =>
    {
      var elapsed = System.Diagnostics.Stopwatch.StartNew();
      try
      {
        while (!session.IsDisposed && !session.Preview.HasFrame)
        {
          if (session.IsDisposed)
            throw new OperationCanceledException();
          if (session.Error != null || elapsed.Elapsed.TotalSeconds > 10)
            throw new IOException(
              session.Error ?? "The camera is not sending video. Reconnect it before the next run."
            );
          await Task.Delay(10);
        }
        if (session.IsDisposed)
          throw new OperationCanceledException();
        var prepared = new FfmpegCameraRecording(
          _executable,
          null,
          Path.Combine(directory, Guid.NewGuid().ToString("N") + ".mp4.partial"),
          device,
          session.RecordingProfile,
          session.SourceSize,
          WebcamRecordingStore.MaximumCaptureBytes
        );
        CaptureDiagnostics.Record(
          "recording.prepare.complete",
          new { backend = "ffmpeg", elapsedMs = elapsed.Elapsed.TotalMilliseconds }
        );
        return prepared;
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        CaptureDiagnostics.Record("recording.prepare.failed", new { backend = "ffmpeg" }, exception);
        throw;
      }
    });
  }

  public async Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0)
  {
    if (_recording != null || _session?.Preview.HasFrame != true)
      throw new IOException("The camera is not sending video. Reconnect it before the next run.");
    if (_session.Error != null)
      throw new IOException(_session.Error);
    PrepareNextRecording();
    Task<FfmpegCameraRecording> prepared = _standby;
    _standby = null;
    FfmpegCameraSession session = _session;
    FfmpegCameraRecording recording = await prepared;
    try
    {
      if (session != _session || session.IsDisposed || session.Error != null)
        throw new IOException(session.Error ?? "Camera capture was stopped before recording could start.");
      recording.Attach(runId, path, maxBytes, startTimestampTicks);
      session.SetRecording(recording);
      _recording = recording;
    }
    catch
    {
      await recording.CancelAsync();
      throw;
    }
  }

  public Task<WebcamRecording> EndAsync() => EndAsync(true);

  private async Task<WebcamRecording> EndAsync(bool prepareNext)
  {
    FfmpegCameraRecording recording = _recording;
    _recording = null;
    _session?.SetRecording(null);
    if (prepareNext)
      PrepareNextRecording();
    return recording == null ? null : await recording.FinishAsync();
  }

  public async Task DisarmAsync()
  {
    try
    {
      WebcamRecordingStore.Discard(await EndAsync(false));
    }
    finally
    {
      _session?.Dispose();
      _session = null;
      _profile = null;
      Task<FfmpegCameraRecording> standby = _standby;
      _standby = null;
      await DiscardStandby(standby);
    }
  }

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
    catch (Exception exception)
    {
      CaptureDiagnostics.Record("recording.prepare.discarded", new { backend = "ffmpeg" }, exception);
    }
  }
}
