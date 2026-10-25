using System;
using HarmonyLib;
using TUFReplay.Composition;
using TUFReplay.Replay.Sessions;

namespace TUFReplay.Webcam.Recording;

[HarmonyPatch]
internal static class WebcamLevelPatches
{
  // Opening a custom level (in the editor or from the custom level list) readies
  // the camera encoder for its runs.
  [HarmonyPatch(typeof(scnGame), "LoadLevel")]
  [HarmonyPostfix]
  private static void OnLoadLevelPostfix(bool __result)
  {
    try
    {
      if (__result && !ReplaySessionService.HasActiveContext)
        FeatureRegistry.WebcamRecording?.SetLevelOpen(true);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnLoadLevelPostfix), exception);
    }
  }
}
