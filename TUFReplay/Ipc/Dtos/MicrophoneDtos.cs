using System.Collections.Generic;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Ipc.Dtos;

public sealed class MicrophoneDeviceDto
{
  public string Id;
  public string Name;
  public int MinFrequency;
  public int MaxFrequency;

  public static MicrophoneDeviceDto From(MicrophoneDeviceInfo device) =>
    new MicrophoneDeviceDto
    {
      Id = device.Id,
      Name = device.Name,
      MinFrequency = device.MinFrequency,
      MaxFrequency = device.MaxFrequency,
    };
}

public sealed class MicrophoneDevicesResponseDto
{
  public List<MicrophoneDeviceDto> Devices;
  public string SelectedDeviceId;

  public static MicrophoneDevicesResponseDto From(MicrophoneDevicesState state)
  {
    var devices = new List<MicrophoneDeviceDto>(state.Devices.Count);
    foreach (MicrophoneDeviceInfo device in state.Devices)
      devices.Add(MicrophoneDeviceDto.From(device));
    return new MicrophoneDevicesResponseDto { Devices = devices, SelectedDeviceId = state.SelectedDeviceId };
  }
}
