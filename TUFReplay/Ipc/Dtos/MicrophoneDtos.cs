using System.Collections.Generic;

namespace TUFReplay.Ipc.Dtos;

public sealed class MicrophoneDeviceDto
{
  public string Id;
  public string Name;
  public int MinFrequency;
  public int MaxFrequency;
}

public sealed class MicrophoneDevicesResponseDto
{
  public List<MicrophoneDeviceDto> Devices;
  public string SelectedDeviceId;
}
