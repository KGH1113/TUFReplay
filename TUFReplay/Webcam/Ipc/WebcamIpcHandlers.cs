using System;
using Newtonsoft.Json.Linq;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Ipc;

public static class WebcamIpcHandlers
{
  public static object GetState(JsonCommand request)
  {
    JToken refresh = (request?.Payload as JObject)?["refreshDevices"];
    bool force = refresh?.Type == JTokenType.Boolean && refresh.Value<bool>();
    return FeatureRegistry.WebcamRecording?.GetState(refreshDevices: true, forceDeviceRefresh: force)
      ?? IpcDomainError.Create(
        "webcam_unavailable",
        "Camera settings are unavailable. Restart the game and try again."
      );
  }

  public static object UpdateSettings(JsonCommand request)
  {
    try
    {
      if (!WebcamSettingsPatch.TryParse(request?.Payload as JObject, out WebcamSettingsPatch patch))
        return IpcDomainError.Create("invalid_webcam_settings", "Check the camera setting values and try again.");
      var feature = FeatureRegistry.WebcamRecording;
      if (feature == null)
        return IpcDomainError.Create(
          "webcam_unavailable",
          "Camera settings are unavailable. Restart the game and try again."
        );
      if (!feature.TryUpdate(patch, out string message))
        return IpcDomainError.Create("webcam_settings_locked", message);
      return feature.GetState();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Webcam/IPC", exception);
      return IpcDomainError.Create("webcam_settings_failed", "Camera settings could not be saved. Try again shortly.");
    }
  }
}
