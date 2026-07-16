using System;
using AdofaiIpc.Core;
using TUFReplay.Application.Microphone;
using TUFReplay.Ipc.Dtos;

namespace TUFReplay.Features.Ipc;

public static class MicrophoneIpcHandlers
{
  public static object GetDevices(IpcRequest request)
  {
    try
    {
      return MicrophoneDevicesResponseDto.From(MicrophoneDeviceService.GetState());
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[IPC] Microphone device query failed: " + exception.GetType().Name);
      return IpcDomainError.Create("microphone_device_query_failed", "Available microphone devices could not be read.");
    }
  }

  public static object SelectDevice(IpcRequest request)
  {
    if (!IpcParams.TryNullableString(request, "deviceId", out string deviceId))
      return IpcDomainError.Create("invalid_microphone_device_id", "deviceId must be a string or null.");

    try
    {
      if (!MicrophoneDeviceService.TrySelect(deviceId, out MicrophoneDevicesState state, out bool changed))
        return IpcDomainError.Create("microphone_device_not_found", "The selected microphone device is not available.");

      if (changed)
        Main.Instance?.Log("[Microphone] Input device selection updated.");
      return MicrophoneDevicesResponseDto.From(state);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[IPC] Microphone device selection failed: " + exception.GetType().Name);
      return IpcDomainError.Create(
        "microphone_device_selection_failed",
        "The microphone device selection could not be saved."
      );
    }
  }
}
