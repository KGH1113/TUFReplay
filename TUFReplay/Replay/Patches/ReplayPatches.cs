using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Replay.Levels;
using TUFReplay.Replay.Patches;
using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Timeline;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Replay.Patches;

[HarmonyPatch]
public static class ReplayInputPatches
{
  private static bool IsActive => ReplayFeature.Instance != null && ReplayFeature.Instance.Active;
  private static bool ShouldHideTimelineRestartVisuals => IsActive && ReplaySessionService.IsTimelineRestartPending;

  [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
  [HarmonyPostfix]
  private static void OnTogglePauseGamePostfix(scrController __instance)
  {
    if (IsActive && __instance != null)
      ReplaySessionService.OnNativeInputPauseChanged(__instance.paused);
  }

  [HarmonyPatch(typeof(scrController), nameof(scrController.Scrub), new[] { typeof(int), typeof(bool) })]
  [HarmonyPrefix]
  private static void OnScrubPrefix(ref bool forceDontStartMusicFourTilesBefore)
  {
    if (ShouldHideTimelineRestartVisuals)
      forceDontStartMusicFourTilesBefore = true;
  }

  [HarmonyPatch(typeof(scrConductor), nameof(scrConductor.PlayHitTimes))]
  [HarmonyPrefix]
  private static void OnPlayHitTimesPrefix(scrConductor __instance, out bool __state)
  {
    __state = __instance.fastTakeoff;
    if (ShouldHideTimelineRestartVisuals)
      __instance.fastTakeoff = true;
  }

  [HarmonyPatch(typeof(scrConductor), nameof(scrConductor.PlayHitTimes))]
  [HarmonyPostfix]
  private static void OnPlayHitTimesPostfix(scrConductor __instance, bool __state)
  {
    __instance.fastTakeoff = __state;
  }

  [HarmonyPatch(typeof(scrUIController), nameof(scrUIController.SetToBlack))]
  [HarmonyPrefix]
  private static bool OnSetToBlackPrefix(scrUIController __instance)
  {
    if (!ShouldHideTimelineRestartVisuals)
      return true;

    __instance.SetToTransparent();
    return false;
  }

  [HarmonyPatch(typeof(scrUIController), nameof(scrUIController.FadeFromBlack))]
  [HarmonyPrefix]
  private static bool OnFadeFromBlackPrefix(scrUIController __instance)
  {
    if (!ShouldHideTimelineRestartVisuals)
      return true;

    __instance.SetToTransparent();
    return false;
  }

  [HarmonyPatch(typeof(scrCountdown), "Update")]
  [HarmonyPrefix]
  private static bool OnCountdownUpdatePrefix(scrCountdown __instance)
  {
    if (!ShouldHideTimelineRestartVisuals)
      return true;

    __instance.CancelGo();
    return false;
  }

  [HarmonyPatch(typeof(scrCountdown), nameof(scrCountdown.ShowGetReady))]
  [HarmonyPrefix]
  private static bool OnShowGetReadyPrefix(scrCountdown __instance)
  {
    if (!ShouldHideTimelineRestartVisuals)
      return true;

    __instance.CancelGo();
    return false;
  }

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
      ReplayLevelOpenService.Tick();
      if (!ReplaySessionService.HasActiveContext)
        return;
      if (!ReplaySessionService.TryGetNativeReplayTimeUs(out long nowUs))
        return;

      ReplaySessionService.TickNativeVisual(nowUs);
      ReplaySessionService.TickMicrophonePlayback(nowUs);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnAsyncInputManagerUpdatePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scnEditor), "DragCamera", new[] { typeof(UnityEngine.Vector3) })]
  [HarmonyPrefix]
  private static bool OnEditorDragCameraPrefix()
  {
    try
    {
      return !IsActive || !ReplayTimelineHud.IsConsumingDragInput;
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnEditorDragCameraPrefix), exception);
      return true;
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

  [HarmonyPatch(typeof(scrPlanet), "AsyncRefreshAngles")]
  [HarmonyPrefix]
  private static bool OnAsyncRefreshAnglesPrefix()
  {
    // Replay input is injected through the native OS path, whose async timestamp belongs to the live
    // OS clock. After a timeline scrub that clock can overwrite the conductor-derived
    // planet angle with a value from the abandoned timeline. Hit-context playback already restores
    // the authoritative replay orbit, so keep the synchronous angle path while a replay is active.
    return !IsActive || !ReplaySessionService.ShouldSuppressGameplayInput();
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

      // Loading another editor scene tears down the current editor through QuitToMenu.
      // That transition is expected while a replay level is being opened.
      if (!ReplayPlaybackCoordinator.ShouldCancelForEditorQuitToMenu)
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
