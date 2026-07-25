using System;
using System.Collections.Generic;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Microphone;
using TUFReplay.Infrastructure.Settings;

namespace TUFReplay.Application.Microphone;

public static class MicrophoneDeviceService
{
  public static MicrophoneDevicesState GetState()
  {
    bool enabled = TUFReplaySettingStore.Current?.MicrophoneEnabled != false;
    return new MicrophoneDevicesState
    {
      Enabled = enabled,
      ToggleLocked = IsToggleLocked(),
      Devices = enabled ? MicrophoneCaptureRuntime.ListDevices() : new List<MicrophoneDeviceInfo>(),
      SelectedDeviceId = TUFReplaySettingStore.Current?.MicrophoneDeviceId,
    };
  }

  public static bool TrySetEnabled(
    bool enabled,
    out MicrophoneDevicesState state,
    out string errorCode,
    out string errorMessage
  )
  {
    state = null;
    errorCode = null;
    errorMessage = null;

    TUFReplaySetting settings = TUFReplaySettingStore.Current;
    if (settings == null || FeatureRegistry.MicrophoneRecording == null)
      return Fail("microphone_unavailable", "Microphone capture is unavailable.", out errorCode, out errorMessage);
    if (settings.MicrophoneEnabled == enabled)
    {
      state = GetState();
      return true;
    }
    if (IsToggleLocked())
      return Fail(
        "microphone_toggle_locked",
        "Finish the current run or microphone calibration before changing microphone access.",
        out errorCode,
        out errorMessage
      );

    bool previous = settings.MicrophoneEnabled;
    settings.MicrophoneEnabled = enabled;
    try
    {
      TUFReplaySettingStore.Save();
      if (!FeatureRegistry.MicrophoneRecording.SetCaptureEnabled(enabled, out string captureError))
        throw new InvalidOperationException(captureError ?? "Microphone capture could not be updated.");
    }
    catch (Exception exception)
    {
      settings.MicrophoneEnabled = previous;
      try
      {
        TUFReplaySettingStore.Save();
        FeatureRegistry.MicrophoneRecording.SetCaptureEnabled(previous, out _);
      }
      catch { }
      return Fail(
        "microphone_toggle_failed",
        "Microphone access could not be updated: " + exception.Message,
        out errorCode,
        out errorMessage
      );
    }

    state = GetState();
    return true;
  }

  public static bool TrySelect(string deviceId, out MicrophoneDevicesState state, out bool changed)
  {
    state = GetState();
    changed = false;
    if (!state.Enabled)
      return false;
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

  private static bool IsToggleLocked()
  {
    if (FeatureRegistry.MicrophoneCalibration?.Active == true)
      return true;
    if (string.Equals(ADOBase.sceneName, "scnEditor", StringComparison.Ordinal))
      return scnEditor.instance?.playMode == true;

    bool gameplayScene =
      string.Equals(ADOBase.sceneName, "scnGame", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCLS", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnCalibration", StringComparison.Ordinal)
      || string.Equals(ADOBase.sceneName, "scnMinesweeper", StringComparison.Ordinal);
    if (!gameplayScene || ADOBase.controller == null)
      return false;

    States state = ADOBase.controller.state;
    return state == States.Countdown || state == States.Checkpoint || state == States.PlayerControl || state == States.Won;
  }

  private static bool Fail(string code, string message, out string errorCode, out string errorMessage)
  {
    errorCode = code;
    errorMessage = message;
    return false;
  }
}
