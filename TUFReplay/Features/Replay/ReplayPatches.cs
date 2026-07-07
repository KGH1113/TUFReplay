using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;
using TUFReplay.Application.Replay;

namespace TUFReplay.Features.Replay;

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

    ReplaySessionService.RequestReplayPitchApplyAfterLevelLoad();
  }

  [JAPatch(
    typeof(scnEditor),
    "Update",
    PatchType.Postfix,
    true
  )]
  private static void OnScnEditorUpdatePostfix()
  {
    ReplaySessionService.TickReplayPitchEditorApply();
  }

  [JAPatch(
    typeof(AsyncInputManager),
    "Update",
    PatchType.Postfix,
    true
  )]
  private static void OnAsyncInputManagerUpdatePostfix()
  {
    if (!ReplaySessionService.HasActiveContext) return;
    if (!ReplaySessionService.TryGetNativeReplayTimeUs(out long nowUs)) return;

    ReplaySessionService.TickNativeVisual(nowUs);
  }

  [JAPatch(
    typeof(scrController),
    "PlayerControl_Update",
    PatchType.Postfix,
    true
  )]
  private static void OnPlayerControlUpdatePostfix(scrController __instance)
  {
    ReplaySessionService.TickHitContextPlayback(__instance);
  }

  [JAPatch(
    typeof(scrController),
    "UpdateFreeroam",
    PatchType.Prefix,
    true
  )]
  private static bool OnUpdateFreeroamPrefix(scrController __instance)
  {
    return !ReplaySessionService.ShouldBlockFreeroam(__instance);
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
    if (!ReplaySessionService.ShouldBlockOriginalHit()) return true;

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
    if (!ReplaySessionService.ShouldSuppressReplayMarkFail()) return true;

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
    ReplaySessionService.OnStateChanged((States)newState);
  }

  [JAPatch(
    typeof(scnEditor),
    "QuitToMenu",
    PatchType.Prefix,
    true
  )]
  private static void OnScnEditorQuitToMenuPrefix()
  {
    ReplaySessionService.StopActiveReplay("editor_quit_to_menu");
  }
}
