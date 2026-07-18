using System;
using System.Collections.Generic;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Microphone;

public interface IMicrophoneCaptureBackend : IDisposable
{
  void RequestPermission();
  List<MicrophoneDeviceInfo> ListDevices();
  bool Arm(string deviceId, out string error);
  MicrophoneArmStatus GetArmStatus();
  bool BeginRun(string runId, string tempPath, out string error);
  CapturedMicrophoneRecording EndRun();
  void Tick();
  void Disarm();
}
