using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Recording.Input;
using TUFReplay.Shared.Capture;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Webcam.Capture;

internal sealed class MacOsWebcamCaptureBackend : IWebcamCaptureBackend
{
  private readonly MacOsCaptureHelperConnection _helper = new MacOsCaptureHelperConnection();
  private WebcamRecording _run;
  private MacOsMachTimeConverter _clock;
  private WebcamCaptureProfile _profile;
  public CameraPreviewBuffer Preview { get; private set; }

  public Task<List<WebcamDevice>> ListDevicesAsync()
  {
    var devices = new List<WebcamDevice>();
    foreach (JToken item in _helper.Send(new JObject { ["command"] = "cameraDevices" })["devices"] ?? new JArray())
      devices.Add(new WebcamDevice { Id = (string)item["id"], Name = (string)item["name"] });
    return Task.FromResult(devices);
  }

  public Task ArmAsync(string deviceId, WebcamCaptureProfile profile)
  {
    Preview?.Dispose();
    Preview = new CameraPreviewBuffer(CameraPreviewBuffer.MaxWidth, CameraPreviewBuffer.MaxHeight);
    _helper.Send(
      new JObject
      {
        ["command"] = "cameraArm",
        ["deviceId"] = deviceId,
        ["width"] = profile.Width,
        ["height"] = profile.Height,
        ["frameRate"] = profile.FrameRate,
        ["bitRate"] = profile.BitRate,
        ["previewPath"] = Preview.Path,
      }
    );
    _profile = profile;
    return Task.CompletedTask;
  }

  public Task BeginAsync(string runId, string path, long maxBytes)
  {
    _clock = MacOsMachTimeConverter.CaptureSystemClock();
    _helper.Send(
      new JObject
      {
        ["command"] = "cameraBegin",
        ["runId"] = runId,
        ["path"] = path,
        ["maxBytes"] = maxBytes,
      }
    );
    _run = new WebcamRecording
    {
      RunId = runId,
      FilePath = path,
      Width = _profile.Width,
      Height = _profile.Height,
      FrameRate = _profile.FrameRate,
    };
    return Task.CompletedTask;
  }

  public Task<WebcamRecording> EndAsync()
  {
    WebcamRecording recording = _run;
    _run = null;
    if (recording == null)
      return Task.FromResult<WebcamRecording>(null);
    try
    {
      JObject response = _helper.Send(new JObject { ["command"] = "cameraEnd" });
      ulong firstHost = (ulong?)response["firstFrameHostTime"] ?? 0;
      recording.DurationUs = (long?)response["durationUs"] ?? 0;
      if (firstHost == 0 || recording.DurationUs <= 0)
        throw new IOException("The camera did not deliver any frames. Check that another app is not using it.");
      recording.CaptureStartTimestampTicks = _clock.ToStopwatchTicks(firstHost);
      recording.DeviceId = (string)response["deviceId"];
      recording.SizeLimited = (bool?)response["sizeLimited"] ?? false;
      recording.Width = (int?)response["width"] ?? recording.Width;
      recording.Height = (int?)response["height"] ?? recording.Height;
      return Task.FromResult(recording);
    }
    catch
    {
      WebcamRecordingStore.Discard(recording);
      throw;
    }
  }

  public Task DisarmAsync()
  {
    _helper.Send(new JObject { ["command"] = "cameraDisarm" });
    Preview?.Dispose();
    Preview = null;
    WebcamRecordingStore.Discard(_run);
    _run = null;
    return Task.CompletedTask;
  }

  internal void LogDiagnosticSnapshot(string reason)
  {
    try
    {
      JObject response = _helper.Send(new JObject { ["command"] = "cameraDiagnostics" });
      CaptureDiagnostics.Record("capture.native.snapshot", new { reason, response });
    }
    catch (Exception exception)
    {
      CaptureDiagnostics.Record("capture.native.snapshot.failed", new { reason }, exception);
    }
  }

  public void Dispose()
  {
    _helper.Dispose();
    Preview?.Dispose();
    Preview = null;
  }
}
