using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

  public async Task ArmAsync(string deviceId, WebcamCaptureProfile profile)
  {
    await DisarmAsync();
    List<WebcamDevice> devices = await ListDevicesAsync();
    WebcamDevice device =
      deviceId == null ? devices.FirstOrDefault() : devices.FirstOrDefault(item => item.Id == deviceId);
    if (device == null)
      throw new IOException("The selected camera is unavailable. Reconnect it or select another camera.");
    _deviceId = device.Id;
    _profile = profile;
    _session = new FfmpegCameraSession(_executable, _deviceId, profile);
  }

  public Task BeginAsync(string runId, string path, long maxBytes)
  {
    if (_recording != null || _session?.Preview.HasFrame != true)
      throw new IOException("The camera is not sending video. Reconnect it before the next run.");
    if (_session.Error != null)
      throw new IOException(_session.Error);
    _recording = new FfmpegCameraRecording(
      _executable,
      runId,
      path,
      _deviceId,
      _session.RecordingProfile,
      _session.SourceSize,
      maxBytes
    );
    _session.SetRecording(_recording);
    return Task.CompletedTask;
  }

  public async Task<WebcamRecording> EndAsync()
  {
    FfmpegCameraRecording recording = _recording;
    _recording = null;
    _session?.SetRecording(null);
    return recording == null ? null : await recording.FinishAsync();
  }

  public async Task DisarmAsync()
  {
    try
    {
      WebcamRecordingStore.Discard(await EndAsync());
    }
    finally
    {
      _session?.Dispose();
      _session = null;
      _profile = null;
    }
  }

  public void Dispose()
  {
    _session?.Dispose();
    _session = null;
  }
}
