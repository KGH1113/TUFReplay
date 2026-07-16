using System;
using TUFReplay.Infrastructure.Settings;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Application.Microphone;

public static class MicrophoneDeviceService
{
  public static MicrophoneDevicesState GetState()
  {
    return new MicrophoneDevicesState
    {
      Devices = UnityMicrophoneDeviceProvider.ListDevices(),
      SelectedDeviceId = TUFReplaySettingStore.Current.MicrophoneDeviceId,
    };
  }

  public static bool TrySelect(string deviceId, out MicrophoneDevicesState state, out bool changed)
  {
    state = GetState();
    changed = false;
    if (
      deviceId != null
      && !state.Devices.Exists(device => string.Equals(device.Id, deviceId, StringComparison.Ordinal))
    )
      return false;

    string previousDeviceId = TUFReplaySettingStore.Current.MicrophoneDeviceId;
    if (string.Equals(previousDeviceId, deviceId, StringComparison.Ordinal))
      return true;

    TUFReplaySettingStore.Current.MicrophoneDeviceId = deviceId;
    try
    {
      TUFReplaySettingStore.Save();
    }
    catch
    {
      TUFReplaySettingStore.Current.MicrophoneDeviceId = previousDeviceId;
      throw;
    }

    state.SelectedDeviceId = deviceId;
    changed = true;
    return true;
  }
}
