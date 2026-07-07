using System;
using JALib.Core.Patch;
using MonsterLove.StateMachine;
using TUFReplay.Application.Recording;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Features.Recording;

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

  [JAPatch(typeof(scrController), "Countdown_Update", PatchType.Postfix, true)]
  private static void OnCountdownUpdatePostfix()
  {
    SampleNativeInput();
  }

  [JAPatch(typeof(scrController), "Checkpoint_Update", PatchType.Postfix, true)]
  private static void OnCheckpointUpdatePostfix()
  {
    SampleNativeInput();
  }

  [JAPatch(typeof(scrController), "PlayerControl_Update", PatchType.Postfix, true)]
  private static void OnPlayerControlUpdatePostfix()
  {
    SampleNativeInput();
  }

  [JAPatch(typeof(scrController), "Won_Update", PatchType.Postfix, true)]
  private static void OnWonUpdatePostfix()
  {
    SampleNativeInput();
  }

  private static void SampleNativeInput()
  {
    if (!IsActive) return;

    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording || !session.IsCapturingInput) return;
    if (!UnityEngine.Application.isFocused) return;

    RecordInputTracker.Sample(session);
  }

  public static bool OnScrPlayerHitPrefix(scrPlayer __instance, bool isAuto, ref bool __result)
  {
    if (!IsActive) return true;
    if (!ShouldCaptureHitContext(__instance)) return true;

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
    if (session == null || !session.IsRecording) return true;

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
    if (recording == null || !recording.Active) return;

    switch (newState)
    {
      case States.Countdown:
        if (!recording.Session.IsRecording) return;

        if (!RecordingGuard.CanRecord(out string reason))
        {
          recording.StopSession();
          Main.Instance.Log("[Recording] Input capture skipped. reason=" + reason);
          return;
        }

        recording.Session.StartInputCapture();
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

  [JAPatch(
    typeof(scnEditor),
    "SwitchToEditMode",
    PatchType.Postfix,
    true,
    ArgumentTypesType = new[] { typeof(bool) }
  )]
  private static void OnSwitchToEditModePostfix(bool clsToEditor)
  {
    if (!IsActive) return;

    RecordingFeature.Instance?.OnReturnedToEditor();
  }

  private static bool ShouldCaptureHitContext(scrPlayer player)
  {
    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording) return false;
    if (player == null || scrController.instance == null) return false;
    if (scrController.instance.playerOne == null || player != scrController.instance.playerOne) return false;
    if (ADOBase.isLevelEditor && ADOBase.controller != null && ADOBase.controller.paused) return false;

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
      CurFreeRoamSection = controller.curFreeRoamSection
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
    if (_hitFloor != currentHitFloor) return false;
    if (_hitMarginsCount == null || currentHitMarginsCount == null) return false;
    if (_hitMarginsCount.Length != currentHitMarginsCount.Length) return false;

    for (int i = 0; i < _hitMarginsCount.Length; i++)
    {
      if (_hitMarginsCount[i] != currentHitMarginsCount[i]) return false;
    }

    return true;
  }
}
