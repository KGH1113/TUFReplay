using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;

namespace TUFReplay.Features.Gameplay;

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
