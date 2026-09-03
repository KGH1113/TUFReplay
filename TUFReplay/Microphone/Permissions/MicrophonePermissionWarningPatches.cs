using System;
using HarmonyLib;

namespace TUFReplay.Microphone.Permissions;

[HarmonyPatch]
internal static class MicrophonePermissionWarningPatches
{
  [HarmonyPatch(typeof(scnGame), "LoadLevel")]
  [HarmonyPostfix]
  private static void OnLoadLevelPostfix(bool __result)
  {
    try
    {
      scnEditor editor = scnEditor.instance;
      if (!__result || editor == null || editor.playMode || !editor.isLoading)
        return;

      MicrophonePermissionWarningCoordinator.NotifyEditorLevelLoaded();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnLoadLevelPostfix), exception);
    }
  }
}
