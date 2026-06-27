using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;

namespace TUFReplay.Replay;

public static class ReplayInputPatches
{
  [JAPatch(
    typeof(scnGame),
    "LoadLevel",
    PatchType.Postfix,
    true
  )]
  private static void OnScnGameLoadLevelPostfix(bool __result)
  {
    if (!__result) return;

    ReplayService.RequestReplayPitchApplyAfterLevelLoad();
  }

  [JAPatch(
    typeof(scnEditor),
    "Update",
    PatchType.Postfix,
    true
  )]
  private static void OnScnEditorUpdatePostfix()
  {
    ReplayService.TickReplayPitchEditorApply();
  }

  [JAPatch(
    typeof(AsyncInputManager),
    "Update",
    PatchType.Postfix,
    true
  )]
  private static void OnAsyncInputManagerUpdatePostfix()
  {
    if (!ReplayService.HasActiveContext) return;
    if (!ReplayService.TryGetNativeReplayTimeUs(out long nowUs)) return;

    ReplayService.TickNativeVisual(nowUs);
  }

  [JAPatch(
    typeof(scrController),
    "PlayerControl_Update",
    PatchType.Postfix,
    true
  )]
  private static void OnPlayerControlUpdatePostfix(scrController __instance)
  {
    ReplayService.TickHitContextPlayback(__instance);
  }

  [JAPatch(
    typeof(scrController),
    "UpdateFreeroam",
    PatchType.Prefix,
    true
  )]
  private static bool OnUpdateFreeroamPrefix(scrController __instance)
  {
    return !ReplayService.ShouldBlockFreeroam(__instance);
  }

  [JAPatch(
    typeof(scrPlayer),
    "Hit",
    PatchType.Prefix,
    true,
    ArgumentTypesType = new[] { typeof(bool) }
  )]
  private static bool OnScrPlayerHitPrefix(ref bool __result)
  {
    if (!ReplayService.ShouldBlockOriginalHit()) return true;

    __result = false;
    return false;
  }

  [JAPatch(
    typeof(scrPlanet),
    "MarkFail",
    PatchType.Prefix,
    true
  )]
  private static bool OnScrPlanetMarkFailPrefix(ref scrMissIndicator __result)
  {
    if (!ReplayService.ShouldSuppressReplayMarkFail()) return true;

    __result = null;
    return false;
  }

  [JAPatch(
    typeof(StateBehaviour),
    "ChangeState",
    PatchType.Postfix,
    true,
    ArgumentTypesType = new[] { typeof(Enum) }
  )]
  private static void OnChangeStatePostfix(Enum newState)
  {
    ReplayService.OnStateChanged((States)newState);
  }

  [JAPatch(
    typeof(scnEditor),
    "QuitToMenu",
    PatchType.Prefix,
    true
  )]
  private static void OnScnEditorQuitToMenuPrefix()
  {
    ReplayService.StopActiveReplay("editor_quit_to_menu");
  }
}
