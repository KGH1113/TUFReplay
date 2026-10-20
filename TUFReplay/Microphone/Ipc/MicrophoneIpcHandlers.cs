using System;
using System.IO;
using TUFReplay.Microphone.Devices;
using TUFReplay.Microphone.Ipc;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Timing;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Microphone.Ipc;

public static class MicrophoneIpcHandlers
{
  public static MicrophoneDevicesResponseDto GetDevices()
  {
    try
    {
      return MicrophoneDevicesResponseDto.From(MicrophoneDeviceService.GetState());
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Microphone device query failed: " + exception.GetType().Name);
      throw MicrophoneFailure(exception, "microphone_device_query_failed", "Available microphones could not be read.");
    }
  }

  public static MicrophoneDevicesResponseDto SelectDevice(string deviceId)
  {
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
        throw IpcDomainError.Create(errorCode, errorMessage);

      if (changed)
        Main.Instance?.Log("[Microphone] Input device selection updated.");
      return MicrophoneDevicesResponseDto.From(state);
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Microphone device selection failed: " + exception.GetType().Name);
      throw MicrophoneFailure(
        exception,
        "microphone_device_selection_failed",
        "The microphone selection could not be saved. The previous device is still active."
      );
    }
  }

  public static MicrophoneDevicesResponseDto SetEnabled(bool enabled)
  {
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
        throw IpcDomainError.Create(errorCode, errorMessage);

      Main.Instance?.Log("[Microphone] Input access " + (enabled ? "enabled." : "disabled."));
      return MicrophoneDevicesResponseDto.From(state);
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.Log("[IPC] Microphone access update failed: " + exception.GetType().Name);
      throw MicrophoneFailure(
        exception,
        "microphone_toggle_failed",
        "The microphone setting could not be saved. The previous setting is still active."
      );
    }
  }

  public static MicrophoneTimingSettingsDto SetOffset(int offsetMs)
  {
    if (
      !MicrophoneTimingSettingsService.TrySetOffset(
        offsetMs,
        out MicrophoneTimingSettingsState state,
        out string errorCode,
        out string errorMessage
      )
    )
      throw IpcDomainError.Create(errorCode, errorMessage);
    return MicrophoneTimingSettingsDto.From(state);
  }

  public static MicrophoneTimingSettingsDto SetVolume(int volumeDb)
  {
    if (
      !MicrophoneTimingSettingsService.TrySetVolume(
        volumeDb,
        out MicrophoneTimingSettingsState state,
        out string errorCode,
        out string errorMessage
      )
    )
      throw IpcDomainError.Create(errorCode, errorMessage);
    return MicrophoneTimingSettingsDto.From(state);
  }

  private static AdofaiIpc.IpcCallException MicrophoneFailure(
    Exception exception,
    string fallbackCode,
    string fallbackMessage
  )
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
