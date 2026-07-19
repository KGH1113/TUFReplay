using System.Collections.Generic;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Microphone;

public sealed class MicrophoneDevicesState
{
  public List<MicrophoneDeviceInfo> Devices;
  public string SelectedDeviceId;
}
