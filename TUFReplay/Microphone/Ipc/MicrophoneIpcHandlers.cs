using System;
using AdofaiIpc.Core;
using TUFReplay.Microphone.Devices;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Timing;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Microphone.Ipc;

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

  public static object SetEnabled(IpcRequest request)
  {
    if (!IpcParams.TryBool(request, "enabled", out bool enabled))
      return IpcDomainError.Create("invalid_microphone_enabled", "enabled must be a boolean.");

    try
    {
      if (
        !MicrophoneDeviceService.TrySetEnabled(
          enabled,
          out MicrophoneDevicesState state,
          out string errorCode,
          out string errorMessage
        )
      )
        return IpcDomainError.Create(errorCode, errorMessage);

      Main.Instance?.Log("[Microphone] Input access " + (enabled ? "enabled." : "disabled."));
      return MicrophoneDevicesResponseDto.From(state);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[IPC] Microphone access update failed: " + exception.GetType().Name);
      return IpcDomainError.Create("microphone_toggle_failed", "Microphone access could not be updated.");
    }
  }

  public static object SetOffset(IpcRequest request)
  {
    int? offsetMs = IpcParams.OptionalInt(request, "offsetMs");
    if (!offsetMs.HasValue)
      return IpcDomainError.Create("invalid_microphone_offset", "offsetMs must be an integer.");
    if (
      !MicrophoneTimingSettingsService.TrySetOffset(
        offsetMs.Value,
        out MicrophoneTimingSettingsState state,
        out string errorCode,
        out string errorMessage
      )
    )
      return IpcDomainError.Create(errorCode, errorMessage);
    return MicrophoneTimingSettingsDto.From(state);
  }

  public static object SetVolume(IpcRequest request)
  {
    int? volumeDb = IpcParams.OptionalInt(request, "volumeDb");
    if (!volumeDb.HasValue)
      return IpcDomainError.Create("invalid_microphone_volume", "volumeDb must be an integer.");
    if (
      !MicrophoneTimingSettingsService.TrySetVolume(
        volumeDb.Value,
        out MicrophoneTimingSettingsState state,
        out string errorCode,
        out string errorMessage
      )
    )
      return IpcDomainError.Create(errorCode, errorMessage);
    return MicrophoneTimingSettingsDto.From(state);
  }
}
