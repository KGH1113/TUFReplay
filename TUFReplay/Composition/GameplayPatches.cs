using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Recording.Patches;
using TUFReplay.Replay.Patches;

namespace TUFReplay.Composition;

[HarmonyPatch]
public static class GameplayPatches
{
  [HarmonyPatch(typeof(scrPlayer), "Hit", new[] { typeof(bool) })]
  [HarmonyPrefix]
  private static bool OnScrPlayerHitPrefix(scrPlayer __instance, bool isAuto, ref bool __result)
  {
    try
    {
      if (!ReplayInputPatches.OnScrPlayerHitPrefix(ref __result))
        return false;

      return RecordingPatches.OnScrPlayerHitPrefix(__instance, isAuto, ref __result);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScrPlayerHitPrefix), exception);
      return true;
    }
  }

  [HarmonyPatch(typeof(scrPlayer), "Hit", new[] { typeof(bool) })]
  [HarmonyPostfix]
  private static void OnScrPlayerHitPostfix(scrPlayer __instance)
  {
    try
    {
      RecordingPatches.OnScrPlayerHitPostfix(__instance);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScrPlayerHitPostfix), exception);
    }
  }

  [HarmonyPatch(typeof(StateBehaviour), "ChangeState", new[] { typeof(Enum) })]
  [HarmonyPostfix]
  private static void OnChangeStatePostfix(Enum newState)
  {
    try
    {
      States state = (States)newState;

      RecordingPatches.OnChangeState(state);
      ReplayInputPatches.OnChangeState(state);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnChangeStatePostfix), exception);
    }
  }
}
