using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

public interface IWebcamCaptureBackend : IDisposable
{
  CameraPreviewBuffer Preview { get; }
  string CaptureWarning { get; }
  Task<List<WebcamDevice>> ListDevicesAsync();
  Task ArmAsync(string deviceId, WebcamCaptureProfile profile, string recordingDirectory = null);
  Task BeginAsync(string runId, string path, long maxBytes, long startTimestampTicks = 0);

  // While a level is open, keeps the next run's encoder open with maxBytes as its
  // limit, so recording starts without waiting for the encoder session.
  Task KeepEncoderReadyAsync(long maxBytes);
  Task ReleaseEncoderAsync();
  Task<WebcamRecording> EndAsync();
  Task DisarmAsync();
}
