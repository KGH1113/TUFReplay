using System.Collections.Generic;
using TUFReplay.Domain.Microphone;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Application.Microphone;

public static class MicrophoneCaptureRuntime
{
  public static IMicrophoneCaptureBackend Backend { get; set; }

  public static List<MicrophoneDeviceInfo> ListDevices()
  {
    return Backend?.ListDevices() ?? UnityMicrophoneDeviceProvider.ListDevices();
  }
}
