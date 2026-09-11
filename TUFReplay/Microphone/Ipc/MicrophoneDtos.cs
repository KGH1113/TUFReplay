using System.Collections.Generic;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Timing;

namespace TUFReplay.Microphone.Ipc;

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
  public bool Enabled;
  public bool ToggleLocked;
  public List<MicrophoneDeviceDto> Devices;
  public string SelectedDeviceId;
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;

  public static MicrophoneDevicesResponseDto From(MicrophoneDevicesState state)
  {
    var devices = new List<MicrophoneDeviceDto>(state.Devices.Count);
    foreach (MicrophoneDeviceInfo device in state.Devices)
      devices.Add(MicrophoneDeviceDto.From(device));
    return new MicrophoneDevicesResponseDto
    {
      Enabled = state.Enabled,
      ToggleLocked = state.ToggleLocked,
      Devices = devices,
      SelectedDeviceId = state.SelectedDeviceId,
      MicrophoneOffsetMs = state.MicrophoneOffsetMs,
      MicrophoneVolumeDb = state.MicrophoneVolumeDb,
    };
  }
}

public sealed class MicrophoneTimingSettingsDto
{
  public int MicrophoneOffsetMs;
  public int MicrophoneVolumeDb;

  public static MicrophoneTimingSettingsDto From(MicrophoneTimingSettingsState state) =>
    new MicrophoneTimingSettingsDto
    {
      MicrophoneOffsetMs = state.MicrophoneOffsetMs,
      MicrophoneVolumeDb = state.MicrophoneVolumeDb,
    };
}
