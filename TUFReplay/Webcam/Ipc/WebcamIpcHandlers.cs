using System;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Ipc;

public static class WebcamIpcHandlers
{
  public static WebcamStateDto GetState(bool refreshDevices)
  {
    return FeatureRegistry.WebcamRecording?.GetState(refreshDevices: true, forceDeviceRefresh: refreshDevices)
      ?? throw IpcDomainError.Create(
        "webcam_unavailable",
        "Camera settings are unavailable. Restart the game and try again."
      );
  }

  public static WebcamStateDto UpdateSettings(WebcamSettings settings)
  {
    try
    {
      if (!WebcamSettingsPatch.TryCreate(settings, out WebcamSettingsPatch patch))
        throw IpcDomainError.Create("invalid_webcam_settings", "Check the camera setting values and try again.");
      var feature = FeatureRegistry.WebcamRecording;
      if (feature == null)
        throw IpcDomainError.Create(
          "webcam_unavailable",
          "Camera settings are unavailable. Restart the game and try again."
        );
      if (!feature.TryUpdate(patch, out string message))
        throw IpcDomainError.Create("webcam_settings_locked", message);
      return feature.GetState();
    }
    catch (Exception exception) when (!(exception is AdofaiIpc.IpcCallException))
    {
      Main.Instance?.LogException("Webcam/IPC", exception);
      throw IpcDomainError.Create("webcam_settings_failed", "Camera settings could not be saved. Try again shortly.");
    }
  }
}
