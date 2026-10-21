using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

public interface IWebcamCaptureBackend : IDisposable
{
  CameraPreviewBuffer Preview { get; }
  Task<List<WebcamDevice>> ListDevicesAsync();
  Task ArmAsync(string deviceId, WebcamCaptureProfile profile, string recordingDirectory = null);
  Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0);
  Task<WebcamRecording> EndAsync();
  Task DisarmAsync();
}
