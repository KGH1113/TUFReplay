using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Recording.Patches;
using TUFReplay.Replay.Patches;
using TUFReplay.Shared.Compatibility;

namespace TUFReplay.Composition;

// Hit runs inside ADOFAI's async-input judgment loop, including on official levels.
// Read isAuto by position instead of through __args, which makes Harmony allocate an
// object[] and box both arguments on every call.
public static class ScrPlayerHitPatch
{
  internal static bool OnPrefix(scrPlayer instance, bool isAuto, ref bool result)
  {
    try
    {
      if (!ReplayInputPatches.OnScrPlayerHitPrefix(ref result))
        return false;

      return RecordingPatches.OnScrPlayerHitPrefix(instance, isAuto, ref result);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnPrefix), exception);
      return true;
    }
  }

  internal static void OnPostfix(scrPlayer instance)
  {
    try
    {
      RecordingPatches.OnScrPlayerHitPostfix(instance);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnPostfix), exception);
    }
  }
}

// Current builds: Hit(long? hitTick, bool isAuto).
[HarmonyPatch]
internal static class ModernScrPlayerHitPatch
{
  // Also selected when neither overload exists, so the missing method still fails loudly.
  [HarmonyPrepare]
  private static bool Prepare() =>
    AdofaiRuntimeCompatibility.ModernPlayerHitMethod != null
    || AdofaiRuntimeCompatibility.LegacyPlayerHitMethod == null;

  [HarmonyTargetMethod]
  private static System.Reflection.MethodBase TargetMethod() => AdofaiRuntimeCompatibility.ResolvePlayerHitMethod();

  [HarmonyPrefix]
  private static bool Prefix(scrPlayer __instance, bool __1, ref bool __result) =>
    ScrPlayerHitPatch.OnPrefix(__instance, __1, ref __result);

  [HarmonyPostfix]
  private static void Postfix(scrPlayer __instance) => ScrPlayerHitPatch.OnPostfix(__instance);
}

// Older builds: Hit(bool isAuto).
[HarmonyPatch]
internal static class LegacyScrPlayerHitPatch
{
  [HarmonyPrepare]
  private static bool Prepare() =>
    AdofaiRuntimeCompatibility.ModernPlayerHitMethod == null
    && AdofaiRuntimeCompatibility.LegacyPlayerHitMethod != null;

  [HarmonyTargetMethod]
  private static System.Reflection.MethodBase TargetMethod() => AdofaiRuntimeCompatibility.LegacyPlayerHitMethod;

  [HarmonyPrefix]
  private static bool Prefix(scrPlayer __instance, bool __0, ref bool __result) =>
    ScrPlayerHitPatch.OnPrefix(__instance, __0, ref __result);

  [HarmonyPostfix]
  private static void Postfix(scrPlayer __instance) => ScrPlayerHitPatch.OnPostfix(__instance);
}

// Observes the vanilla input-event check inside Hit; see RecordingPatches.
[HarmonyPatch]
internal static class ScrPlayerHitInputEventPatch
{
  [HarmonyPrepare]
  private static bool Prepare() => AdofaiRuntimeCompatibility.PlayerHitInputEventMethod != null;

  [HarmonyTargetMethod]
  private static System.Reflection.MethodBase TargetMethod() => AdofaiRuntimeCompatibility.PlayerHitInputEventMethod;

  [HarmonyPostfix]
  private static void Postfix(scrPlayer __instance, InputEventState __1, bool __result)
  {
    try
    {
      RecordingPatches.OnScrPlayerHitInputEventPostfix(__instance, __1, __result);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(ScrPlayerHitInputEventPatch), exception);
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
