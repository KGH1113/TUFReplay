using JALib.Core.Patch;
using MonsterLove.StateMachine;
using TUFReplay.Application.Replay;

namespace TUFReplay.Features.Replay;

public static class ReplayInputPatches
{
  private static bool IsActive => ReplayFeature.Instance != null && ReplayFeature.Instance.Active;

  [JAPatch(
    typeof(scnGame),
    "LoadLevel",
    PatchType.Postfix,
    true
  )]
  private static void OnScnGameLoadLevelPostfix(bool __result)
  {
    if (!IsActive) return;
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
    if (!IsActive) return;

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
    if (!IsActive) return;
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
    if (!IsActive) return;

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
    if (!IsActive) return true;

    return !ReplaySessionService.ShouldBlockFreeroam(__instance);
  }

  public static bool OnScrPlayerHitPrefix(ref bool __result)
  {
    if (!IsActive) return true;
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
    if (!IsActive) return true;
    if (!ReplaySessionService.ShouldSuppressReplayMarkFail()) return true;

    __result = null;
    return false;
  }

  public static void OnChangeState(States newState)
  {
    if (!IsActive) return;

    ReplaySessionService.OnStateChanged(newState);
  }

  [JAPatch(
    typeof(scnEditor),
    "QuitToMenu",
    PatchType.Prefix,
    true
  )]
  private static void OnScnEditorQuitToMenuPrefix()
  {
    if (!IsActive) return;

    ReplaySessionService.StopActiveReplay("editor_quit_to_menu");
  }
}
