using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;
using TUFReplay.Features.Recording;
using TUFReplay.Features.Replay;

namespace TUFReplay.Features.Gameplay;

public static class GameplayPatches
{
  [JAPatch(typeof(scrPlayer), "Hit", PatchType.Prefix, true, ArgumentTypesType = new[] { typeof(bool) })]
  private static bool OnScrPlayerHitPrefix(scrPlayer __instance, bool isAuto, ref bool __result)
  {
    if (!ReplayInputPatches.OnScrPlayerHitPrefix(ref __result))
      return false;

    return RecordingPatches.OnScrPlayerHitPrefix(__instance, isAuto, ref __result);
  }

  [JAPatch(typeof(StateBehaviour), "ChangeState", PatchType.Postfix, true, ArgumentTypesType = new[] { typeof(Enum) })]
  private static void OnChangeStatePostfix(Enum newState)
  {
    States state = (States)newState;

    RecordingPatches.OnChangeState(state);
    ReplayInputPatches.OnChangeState(state);
  }
}
