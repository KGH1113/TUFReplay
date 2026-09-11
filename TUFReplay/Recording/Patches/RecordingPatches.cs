using System;
using System.Diagnostics;
using HarmonyLib;
using MonsterLove.StateMachine;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Models;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Recording.Patches;

[HarmonyPatch]
public static class RecordingPatches
{
  private static scrFloor _hitFloor;
  private static readonly HitMarginSnapshot HitMargins = new();
  private static bool _pendingHitMarginCapture;
  private static long _conductorUpdatePrefixTicks;
  private static bool? _lastCaptureAllowed;
  private static bool IsActive => RecordingFeature.Instance != null && RecordingFeature.Instance.Active;

  public static void ResetHitContextState()
  {
    _hitFloor = null;
    _pendingHitMarginCapture = false;
    HitMargins.Reset();
    _lastCaptureAllowed = null;
  }

  [HarmonyPatch(typeof(scrConductor), "Update")]
  [HarmonyPrefix]
  private static void OnConductorUpdatePrefix()
  {
    _conductorUpdatePrefixTicks = Stopwatch.GetTimestamp();
  }

  [HarmonyPatch(typeof(scrConductor), "Update")]
  [HarmonyPostfix]
  private static void OnConductorUpdatePostfix(scrConductor __instance)
  {
    try
    {
      if (!IsActive || __instance == null)
        return;
      RecordingSession session = RecordingFeature.Instance?.Session;
      if (session == null || !session.IsRecording || !session.IsCapturingInput)
        return;

      long postfixTicks = Stopwatch.GetTimestamp();
      RecordInputTracker.DrainCapturedTransitions(session);
      bool captureAllowed = IsNativeInputCaptureAllowed();
      ObserveCapturePermissionTransition(session, captureAllowed);
      if (!captureAllowed)
        return;
      float pitch = __instance.song != null ? __instance.song.pitch : 0f;
      double songPosition = __instance.songposition_minusi;
      bool ready =
        __instance.hasSongStarted
        && __instance.song != null
        && __instance.crotchetAtStart > 0d
        && pitch > 0f
        && !double.IsNaN(songPosition)
        && !double.IsInfinity(songPosition)
        && !float.IsNaN(pitch)
        && !float.IsInfinity(pitch);
      session.ObserveInputAnchor(_conductorUpdatePrefixTicks, postfixTicks, songPosition, pitch, ready);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnConductorUpdatePostfix), exception);
    }
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

    RecordingFeature.Instance.TryAnchorMicrophoneTimeline();

    bool captureAllowed = IsNativeInputCaptureAllowed();
    ObserveCapturePermissionTransition(session, captureAllowed);
    RecordInputTracker.SetCaptureWindowActive(captureAllowed);
    if (!captureAllowed)
      return;

    RecordInputTracker.DrainCapturedTransitions(session);
  }

  public static bool OnScrPlayerHitPrefix(scrPlayer __instance, bool isAuto, ref bool __result)
  {
    _pendingHitMarginCapture = false;
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
    int[] currentHitMarginsCount = controller.playerOne.marginTracker.hitMarginsCount;

    if (session.HitContextCount > 0 && PreviousHitWasInvalid(currentHitFloor, currentHitMarginsCount))
    {
      session.RemoveLastHitContext();
    }

    _hitFloor = currentHitFloor;
    HitMargins.Capture(currentHitMarginsCount);
    session.AddHitContext(BuildHitContext(controller, isAuto));
    _pendingHitMarginCapture = true;

    return true;
  }

  public static void OnScrPlayerHitPostfix(scrPlayer __instance)
  {
    if (!_pendingHitMarginCapture)
      return;

    _pendingHitMarginCapture = false;
    if (!ShouldCaptureHitContext(__instance))
      return;

    RecordingSession session = RecordingFeature.Instance?.Session;
    int[] currentHitMarginsCount = scrController.instance?.playerOne?.marginTracker?.hitMarginsCount;
    if (session == null || !HitMargins.TryGetSingleIncrement(currentHitMarginsCount, out int hitMargin))
      return;

    session.SetLastHitContextMargin(hitMargin);
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
        recording.Session.BreakInputTimeline(newState.ToString().ToLowerInvariant());
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
        RecordInputTracker.SetCaptureWindowActive(IsNativeInputCaptureAllowed());
        recording.OnInputCaptureStarted();
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
        recording.Session.BreakInputTimeline("fail");
        RecordingFeature.Instance.OnRunFailed();
        break;
    }
  }

  private static void UpdateNativeInputCaptureState(RecordingSession session, States newState)
  {
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;

    bool captureAllowed = IsNativeInputCaptureAllowed();
    bool active = captureAllowed && IsNativeInputCaptureState(newState);
    if (active)
    {
      RecordInputTracker.SetCaptureWindowActive(true);
      RecordInputTracker.DrainCapturedTransitions(session);
      return;
    }

    if (captureAllowed)
      RecordInputTracker.DrainCapturedTransitions(session);
    RecordInputTracker.SetCaptureWindowActive(false);
  }

  private static bool IsNativeInputCaptureAllowed()
  {
    return UnityEngine.Application.isFocused && !NativeInputUmmWindowInterlock.IsBlocked;
  }

  private static void ObserveCapturePermissionTransition(RecordingSession session, bool captureAllowed)
  {
    if (_lastCaptureAllowed.HasValue && _lastCaptureAllowed.Value != captureAllowed)
      session.BreakInputTimeline(captureAllowed ? "focus_or_umm_resume" : "focus_or_umm_block");
    _lastCaptureAllowed = captureAllowed;
  }

  private static bool IsNativeInputCaptureState(States state)
  {
    return state == States.Countdown
      || state == States.Checkpoint
      || state == States.PlayerControl
      || state == States.Won;
  }

  [HarmonyPatch(typeof(scrController), "TogglePauseGame")]
  [HarmonyPostfix]
  private static void OnTogglePauseGamePostfix(scrController __instance)
  {
    RecordingSession session = RecordingFeature.Instance?.Session;
    if (session == null || !session.IsRecording || !session.IsCapturingInput)
      return;
    if (__instance != null && __instance.paused)
      RecordInputTracker.DrainCapturedTransitions(session);
    session.BreakInputTimeline(__instance != null && __instance.paused ? "pause" : "resume");
    RecordInputTracker.SetCaptureWindowActive(
      __instance != null && !__instance.paused && IsNativeInputCaptureAllowed()
    );
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

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode", new[] { typeof(bool) })]
  [HarmonyPostfix]
  private static void OnSwitchToEditModePostfix()
  {
    try
    {
      if (IsActive)
        RecordingFeature.Instance?.OnEditorReturnCompleted();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnSwitchToEditModePostfix), exception);
    }
  }

  [HarmonyPatch(typeof(scnEditor), "SwitchToEditMode", new[] { typeof(bool) })]
  [HarmonyFinalizer]
  private static Exception OnSwitchToEditModeFinalizer(Exception __exception)
  {
    if (__exception == null)
      return null;

    try
    {
      RecordingFeature.Instance?.OnEditorReturnFailed();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException(nameof(OnSwitchToEditModeFinalizer), exception);
    }
    return __exception;
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

  private static bool PreviousHitWasInvalid(scrFloor currentHitFloor, int[] currentHitMarginsCount)
  {
    if (_hitFloor != currentHitFloor)
      return false;

    return HitMargins.Matches(currentHitMarginsCount);
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
