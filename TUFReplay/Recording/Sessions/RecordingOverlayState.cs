using System;
using System.Collections.Generic;
using System.Reflection;

namespace TUFReplay.Recording.Sessions;

// Recorder bridges are discovered independently of OrbitRender so installing
// the renderer later does not lose the original cumulative counters.
internal static class RecordingOverlayState
{
  internal static Dictionary<string, string> Capture()
  {
    var states = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
      try
      {
        Type bridge = assembly.GetType("JipperResourcePack.KeyViewerContents.OtherModApi.OrbitRenderAPI", false);
        MethodInfo capture = bridge?.GetMethod(
          "CaptureRecordingState",
          BindingFlags.Public | BindingFlags.Static,
          null,
          Type.EmptyTypes,
          null
        );
        if (capture?.ReturnType != typeof(string))
          continue;
        if (capture.Invoke(null, null) is string json && !string.IsNullOrWhiteSpace(json))
          states["JipperResourcePack"] = json;
      }
      catch (Exception exception)
      {
        Main.Instance?.LogException("[Recording] Overlay state capture failed", exception);
      }
    }
    return states;
  }
}
