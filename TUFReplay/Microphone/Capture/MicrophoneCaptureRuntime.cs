using System.Collections.Generic;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Capture;

public static class MicrophoneCaptureRuntime
{
  public static IMicrophoneCaptureBackend Backend { get; set; }

  public static List<MicrophoneDeviceInfo> ListDevices()
  {
    return Backend?.ListDevices() ?? new List<MicrophoneDeviceInfo>();
  }
}
