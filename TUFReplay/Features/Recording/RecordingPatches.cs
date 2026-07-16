using System;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Application.Recording;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Features.Recording;

[HarmonyPatch]
public static class RecordingPatches
{
  private static scrFloor _hitFloor;
  private static int[] _hitMarginsCount;
  private static bool IsActive => RecordingFeature.Instance != null && RecordingFeature.Instance.Active;

  public static void ResetHitContextState()
  {
    _hitFloor = null;
    _hitMarginsCount = null;
  }

  [HarmonyPatch(typeof(scrController), "Countdown_Update")]
  [HarmonyPostfix]
  private static void OnCountdownUpdatePostfix()
  {
    TrySampleNativeInput(nameof(OnCountdownUpdatePostfix));
  }

  [HarmonyPatch(typeof(scrController), "Checkpoint_Update")]
  [HarmonyPostfix]
  private static void OnCheckpointUpdatePostfix()
  {
    TrySampleNativeInput(nameof(OnCheckpointUpdatePostfix));
  }

  [HarmonyPatch(typeof(scrController), "PlayerControl_Update")]
  [HarmonyPostfix]
  private static void OnPlayerControlUpdatePostfix()
  {
    TrySampleNativeInput(nameof(OnPlayerControlUpdatePostfix));
  }

  [HarmonyPatch(typeof(scrController), "Won_Update")]
  [HarmonyPostfix]
  private static void OnWonUpdatePostfix()
  {
    TrySampleNativeInput(nameof(OnWonUpdatePostfix));
  }

  private static void TrySampleNativeInput(string patchName)
  {
    try
    {
      SampleNativeInput();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(patchName, exception);
    }
  }

  private static void SampleNativeInput()
  {
    if (!IsActive)
      return;

    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;

    bool focused = UnityEngine.Application.isFocused;
    RecordInputTracker.SetCaptureWindowActive(focused);
    if (!focused)
      return;

    RecordInputTracker.Sample(session);
  }

  public static bool OnScrPlayerHitPrefix(scrPlayer __instance, bool isAuto, ref bool __result)
  {
    if (!IsActive)
      return true;
    if (!ShouldCaptureHitContext(__instance))
      return true;

    if (!__instance.responsive)
    {
      __result = false;
      return false;
    }

    if (ADOBase.isLevelEditor && ADOBase.controller.paused)
    {
      __result = false;
      return false;
    }

    if (!scrController.instance.playerOne.HitInputEvent(isAuto, InputEventState.Down))
    {
      __result = false;
      return false;
    }

    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording)
      return true;

    scrController controller = scrController.instance;
    scrFloor currentHitFloor = controller.chosenPlanet.currfloor;
    int[] currentHitMarginsCount = CopyHitMargins(controller.playerOne);

    if (session.HitContextCount > 0 && PreviousHitWasInvalid(currentHitFloor, currentHitMarginsCount))
    {
      session.RemoveLastHitContext();
    }

    _hitFloor = currentHitFloor;
    _hitMarginsCount = currentHitMarginsCount;
    session.AddHitContext(BuildHitContext(controller, isAuto));

    return true;
  }

  public static void OnChangeState(States newState)
  {
    RecordingFeature recording = RecordingFeature.Instance;
    if (recording == null || !recording.Active)
      return;

    UpdateNativeInputCaptureState(recording.Session, newState);

    switch (newState)
    {
      case States.Countdown:
      case States.Checkpoint:
        if (!recording.Session.IsRecording)
          return;

        if (!RecordingGuard.CanRecord(out string reason))
        {
          recording.StopSession();
          Main.Instance.Log("[Recording] Input capture skipped. reason=" + reason);
          return;
        }

        if (!recording.PrepareRunForInputCapture())
          return;
        recording.Session.StartInputCapture();
        RecordInputTracker.SetCaptureWindowActive(UnityEngine.Application.isFocused);
        ResetHitContextState();
        break;

      case States.PlayerControl:
        RecordingFeature.Instance.OnGameplayStarted();
        break;

      case States.Won:
        RecordingFeature.Instance.OnClearReached();
        break;

      case States.Fail:
      case States.Fail2:
        RecordingFeature.Instance.OnRunFailed();
        break;
    }
  }

  private static void UpdateNativeInputCaptureState(RecordingSession session, States newState)
  {
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;

    bool focused = UnityEngine.Application.isFocused;
    bool active = focused && IsNativeInputCaptureState(newState);
    if (active)
    {
      RecordInputTracker.SetCaptureWindowActive(true);
      RecordInputTracker.Sample(session);
      return;
    }

    if (focused)
      RecordInputTracker.Sample(session);
    RecordInputTracker.SetCaptureWindowActive(false);
  }

  private static bool IsNativeInputCaptureState(States state)
  {
    return state == States.Countdown
      || state == States.Checkpoint
      || state == States.PlayerControl
      || state == States.Won;
  }

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode", new[] { typeof(bool) })]
  [HarmonyPrefix]
  private static void OnSwitchToEditModePrefix(bool clsToEditor)
  {
    try
    {
      if (!IsActive)
        return;

      SampleNativeInput();
      RecordingFeature.Instance?.OnReturnedToEditor();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnSwitchToEditModePrefix), exception);
    }
  }

  private static bool ShouldCaptureHitContext(scrPlayer player)
  {
    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording)
      return false;
    if (player == null || scrController.instance == null)
      return false;
    if (scrController.instance.playerOne == null || player != scrController.instance.playerOne)
      return false;
    if (ADOBase.isLevelEditor && ADOBase.controller != null && ADOBase.controller.paused)
      return false;

    return true;
  }

  private static RecordedHitContext BuildHitContext(scrController controller, bool isAuto)
  {
    scrPlanet chosenPlanet = controller.chosenPlanet;
    scrPlayer player = controller.playerOne;

    return new RecordedHitContext
    {
      CurrentFloorID = controller.currFloor.seqID,
      CurrAngle = GetCurrentAngle(controller),
      OverloadCounter = player.failBar.overloadCounter,
      NoFailHit = controller.noFailInfiniteMargin,
      IsAuto = isAuto,
      NextFloorAuto = chosenPlanet.currfloor.nextfloor != null && chosenPlanet.currfloor.nextfloor.auto,
      CachedAngle = chosenPlanet.angle,
      TargetExitAngle = chosenPlanet.targetExitAngle,
      MidspinInfiniteMargin = player.midspinInfiniteMargin,
      RDCAuto = RDC.auto,
      CurFreeRoamSection = controller.curFreeRoamSection,
    };
  }

  private static float GetCurrentAngle(scrController controller)
  {
    float angle = (float)(controller.chosenPlanet.angle - controller.chosenPlanet.targetExitAngle);
    if (!controller.playerOne.planetarySystem.isCW)
    {
      angle *= -1f;
    }

    return angle;
  }

  private static int[] CopyHitMargins(scrPlayer player)
  {
    int[] source = player.marginTracker.hitMarginsCount;
    int[] copy = new int[source.Length];
    Array.Copy(source, copy, source.Length);
    return copy;
  }

  private static bool PreviousHitWasInvalid(scrFloor currentHitFloor, int[] currentHitMarginsCount)
  {
    if (_hitFloor != currentHitFloor)
      return false;
    if (_hitMarginsCount == null || currentHitMarginsCount == null)
      return false;
    if (_hitMarginsCount.Length != currentHitMarginsCount.Length)
      return false;

    for (int i = 0; i < _hitMarginsCount.Length; i++)
    {
      if (_hitMarginsCount[i] != currentHitMarginsCount[i])
        return false;
    }

    return true;
  }

  [HarmonyPatch(typeof(scnEditor), "Play")]
  [HarmonyPrefix]
  private static void OnEditorPlayPrefix()
  {
    try
    {
      if (!IsActive)
        return;
      RecordingFeature.Instance.OnEditorPlay();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnEditorPlayPrefix), exception);
    }
  }
}
