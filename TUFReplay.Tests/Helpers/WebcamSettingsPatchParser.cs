using AdofaiIpc;
using Newtonsoft.Json.Linq;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Webcam.Models;

internal static class WebcamSettingsPatchParser
{
  public static bool TryParse(JObject values, out WebcamSettingsPatch patch)
  {
    patch = null;
    try
    {
      var request = ReplayApiMethods.ChangeWebcamSettings.Input.Read(new JObject { ["settings"] = values }.ToString());
      return WebcamSettingsPatch.TryCreate(request.Settings, out patch);
    }
    catch (IpcValidationException)
    {
      return false;
    }
  }
}
