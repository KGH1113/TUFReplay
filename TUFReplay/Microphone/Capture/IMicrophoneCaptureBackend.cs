using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Capture;

public interface IMicrophoneCaptureBackend : IDisposable
{
  void RequestPermission();
  void RefreshPermissionStatus();
  MicrophonePermissionStatus GetPermissionStatus();
  List<MicrophoneDeviceInfo> ListDevices();
  bool Arm(string deviceId, out string error);
  MicrophoneArmStatus GetArmStatus();
  bool BeginRun(string runId, string tempPath, out string error);
  Task<CapturedMicrophoneRecording> EndRunAsync();
  void Tick();
  void Disarm();
}
