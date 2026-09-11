using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Recording.Patches;
using TUFReplay.Replay.Patches;
using TUFReplay.Shared.Compatibility;

namespace TUFReplay.Composition;

[HarmonyPatch]
public static class ScrPlayerHitPatch
{
  [HarmonyTargetMethod]
  private static System.Reflection.MethodBase TargetMethod() => AdofaiRuntimeCompatibility.ResolvePlayerHitMethod();

  [HarmonyPrefix]
  private static bool OnScrPlayerHitPrefix(scrPlayer __instance, object[] __args, ref bool __result)
  {
    try
    {
      if (!ReplayInputPatches.OnScrPlayerHitPrefix(ref __result))
        return false;

      bool isAuto = __args.Length > 0 && __args[__args.Length - 1] is bool value && value;
      return RecordingPatches.OnScrPlayerHitPrefix(__instance, isAuto, ref __result);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScrPlayerHitPrefix), exception);
      return true;
    }
  }

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
}

[HarmonyPatch]
public static class GameplayStatePatch
{
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
