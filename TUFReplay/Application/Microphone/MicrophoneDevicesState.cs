using System.Collections.Generic;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Microphone;

public sealed class MicrophoneDevicesState
{
  public bool Enabled;
  public bool ToggleLocked;
  public List<MicrophoneDeviceInfo> Devices;
  public string SelectedDeviceId;
}
