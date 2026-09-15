using System;
using System.IO;
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
      return MicrophoneFailure(exception, "microphone_device_query_failed", "Available microphones could not be read.");
    }
  }

  public static object SelectDevice(IpcRequest request)
  {
    if (!IpcParams.TryNullableString(request, "deviceId", out string deviceId))
      return IpcDomainError.Create("invalid_microphone_device_id", "deviceId must be a string or null.");

    try
    {
      if (
        !MicrophoneDeviceService.TrySelect(
          deviceId,
          out MicrophoneDevicesState state,
          out bool changed,
          out string errorCode,
          out string errorMessage
        )
      )
        return IpcDomainError.Create(errorCode, errorMessage);

      if (changed)
        Main.Instance?.Log("[Microphone] Input device selection updated.");
      return MicrophoneDevicesResponseDto.From(state);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[IPC] Microphone device selection failed: " + exception.GetType().Name);
      return MicrophoneFailure(
        exception,
        "microphone_device_selection_failed",
        "The microphone selection could not be saved. The previous device is still active."
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
      return MicrophoneFailure(
        exception,
        "microphone_toggle_failed",
        "The microphone setting could not be saved. The previous setting is still active."
      );
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

  private static object MicrophoneFailure(Exception exception, string fallbackCode, string fallbackMessage)
  {
    if (exception is UnauthorizedAccessException)
    {
      return IpcDomainError.Create(
        "microphone_permission_denied",
        "Microphone access is off. Allow TUFReplay Microphone Capture in System Settings."
      );
    }
    if (exception is DllNotFoundException || exception is FileNotFoundException || exception is IOException)
    {
      return IpcDomainError.Create(
        "microphone_helper_unavailable",
        "The microphone helper could not start. Restart the game; if it continues, reinstall TUFReplay."
      );
    }
    return IpcDomainError.Create(fallbackCode, fallbackMessage);
  }
}
