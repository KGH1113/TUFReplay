using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Application.Replay;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Features.Replay;

[HarmonyPatch]
public static class ReplayInputPatches
{
  private static bool IsActive => ReplayFeature.Instance != null && ReplayFeature.Instance.Active;

  [HarmonyPatch(typeof(scnGame), "LoadLevel")]
  [HarmonyPostfix]
  private static void OnScnGameLoadLevelPostfix(bool __result)
  {
    try
    {
      if (!IsActive)
        return;
      if (!__result)
        return;

      ReplaySessionService.RequestReplayPitchApplyAfterLevelLoad();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScnGameLoadLevelPostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scnEditor), "Update")]
  [HarmonyPostfix]
  private static void OnScnEditorUpdatePostfix()
  {
    try
    {
      if (!IsActive)
        return;

      ReplaySessionService.TickReplayPitchEditorApply();
      ReplayPlaybackCoordinator.Tick();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScnEditorUpdatePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode", new[] { typeof(bool) })]
  [HarmonyPostfix]
  private static void OnSwitchToEditModePostfix(bool clsToEditor)
  {
    try
    {
      if (!IsActive)
        return;
      ReplayPlaybackCoordinator.OnReturnedToEditor();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnSwitchToEditModePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(AsyncInputManager), "Update")]
  [HarmonyPostfix]
  private static void OnAsyncInputManagerUpdatePostfix()
  {
    try
    {
      if (!IsActive)
        return;

      UnityMainThread.DrainPending();
      if (!ReplaySessionService.HasActiveContext)
        return;
      if (!ReplaySessionService.TryGetNativeReplayTimeUs(out long nowUs))
        return;

      ReplaySessionService.TickNativeVisual(nowUs);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnAsyncInputManagerUpdatePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scrController), "PlayerControl_Update")]
  [HarmonyPostfix]
  private static void OnPlayerControlUpdatePostfix(scrController __instance)
  {
    try
    {
      if (!IsActive)
        return;

      ReplaySessionService.TickHitContextPlayback(__instance);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnPlayerControlUpdatePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scrPlayer), "ValidInputWasTriggered")]
  [HarmonyPrefix]
  private static bool OnValidInputWasTriggeredPrefix(ref bool __result)
  {
    if (!IsActive || !ReplaySessionService.ShouldSuppressGameplayInput())
      return true;

    __result = false;
    return false;
  }

  [HarmonyPatch(typeof(scrPlayer), "ValidInputWasReleased")]
  [HarmonyPrefix]
  private static bool OnValidInputWasReleasedPrefix(ref bool __result)
  {
    if (!IsActive || !ReplaySessionService.ShouldSuppressGameplayInput())
      return true;

    __result = false;
    return false;
  }

  [HarmonyPatch(typeof(scrPlayer), "HitAutoFloors")]
  [HarmonyPrefix]
  private static bool OnHitAutoFloorsPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrPlayer), "OttoHoldHit")]
  [HarmonyPrefix]
  private static bool OnOttoHoldHitPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrPlayer), "HitHoldFloorsIfStartedAtHold")]
  [HarmonyPrefix]
  private static bool OnHitHoldFloorsIfStartedAtHoldPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrPlayer), "UpdateHoldKeys")]
  [HarmonyPrefix]
  private static bool OnUpdateHoldKeysPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrPlayer), "CheckPreHoldFail")]
  [HarmonyPrefix]
  private static bool OnCheckPreHoldFailPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrPlayer), "CheckPostHoldFail")]
  [HarmonyPrefix]
  private static bool OnCheckPostHoldFailPrefix()
  {
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
  }

  [HarmonyPatch(typeof(scrController), "UpdateFreeroam")]
  [HarmonyPrefix]
  private static bool OnUpdateFreeroamPrefix(scrController __instance)
  {
    try
    {
      if (!IsActive)
        return true;

      return !ReplaySessionService.ShouldBlockFreeroam(__instance);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnUpdateFreeroamPrefix), exception);
      return true;
    }
  }

  public static bool OnScrPlayerHitPrefix(ref bool __result)
  {
    if (!IsActive)
      return true;
    if (!ReplaySessionService.ShouldBlockOriginalHit())
      return true;

    __result = false;
    return false;
  }

  [HarmonyPatch(typeof(scrPlanet), "MarkFail")]
  [HarmonyPrefix]
  private static bool OnScrPlanetMarkFailPrefix(ref scrMissIndicator __result)
  {
    try
    {
      if (!IsActive)
        return true;
      if (!ReplaySessionService.ShouldSuppressReplayMarkFail())
        return true;

      __result = null;
      return false;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScrPlanetMarkFailPrefix), exception);
      return true;
    }
  }

  public static void OnChangeState(States newState)
  {
    if (!IsActive)
      return;

    ReplaySessionService.OnStateChanged(newState);
  }

  [HarmonyPatch(typeof(scnEditor), "QuitToMenu")]
  [HarmonyPrefix]
  private static void OnScnEditorQuitToMenuPrefix()
  {
    try
    {
      if (!IsActive)
        return;

      ReplayPlaybackCoordinator.Fail("editor_quit_to_menu", "The editor was closed during replay.");
      ReplaySessionService.StopActiveReplay("editor_quit_to_menu");
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnScnEditorQuitToMenuPrefix), exception);
    }
  }
}
