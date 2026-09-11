using System.Collections.Generic;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Models;

public sealed class MicrophoneDevicesState
{
  public bool Enabled;
  public bool ToggleLocked;
  public List<MicrophoneDeviceInfo> Devices;
  public string SelectedDeviceId;
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;
}
