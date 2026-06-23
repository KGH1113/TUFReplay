using System;
using System.Collections.Generic;
using MonsterLove.StateMachine;
using TUFHelper.ModScripts.Json;
using TUFReplay.Recording;

namespace TUFReplay.Replay;

public static class ReplayPatches
{
  public static bool GetMainPrefix(ButtonState state, ref int __result)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session?.UsesAsyncInputPlayback == true) return true;
    if (session == null || !session.CanInjectInput()) return true;

    __result = session.GetMainCount(state);
    return false;
  }

  public static void GetMainPostfix(ButtonState state, ref int __result)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session?.UsesAsyncInputPlayback == true)
    {
      int replayResult = session.GetMainCount(state);
      if (replayResult > __result) __result = replayResult;

      if ((state == ButtonState.WentDown || state == ButtonState.WentUp) && __result > 0)
      {
        Replay.LogDebug(
          "Async RDInput.GetMain state=" +
          state +
          ", result=" +
          __result +
          ", frame=" +
          UnityEngine.Time.frameCount
        );
      }

      return;
    }

    if (session?.IsPlaying == true) return;

    RDInputRecorder.CaptureMainState(state, __result);
  }

  public static bool GetStateKeysPrefix(ButtonState state, ref List<AnyKeyCode> __result)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session?.UsesAsyncInputPlayback == true) return true;
    if (session == null || !session.CanInjectInput()) return true;

    __result = new List<AnyKeyCode>();
    session.AddStateKeys(state, __result);
    return false;
  }

  public static void GetStateKeysPostfix(ButtonState state, List<AnyKeyCode> __result)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session?.UsesAsyncInputPlayback == true)
    {
      if (__result == null) return;

      int originalCount = __result.Count;
      session.AddStateKeys(state, __result);

      if ((state == ButtonState.WentDown || state == ButtonState.WentUp) && (__result?.Count ?? 0) > 0)
      {
        Replay.LogDebug(
          "Async RDInput.GetStateKeys state=" +
          state +
          ", count=" +
          __result.Count +
          ", added=" +
          (__result.Count - originalCount) +
          ", frame=" +
          UnityEngine.Time.frameCount
        );
      }

      return;
    }

    if (session?.IsPlaying == true) return;

    RDInputRecorder.CaptureStateKeys(state, __result);
  }

  public static void OnChangeStatePostfix(Enum newState)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session == null || !session.IsPlaying) return;

    switch ((States)newState)
    {
      case States.Won:
      case States.Fail:
      case States.Fail2:
        session.Stop("game state changed to " + newState);
        break;
    }
  }

  public static void LevelPrefabTryToLoadLevelPrefix(LevelListInfoElementJson levelInfo, string levelFilePath = null)
  {
    if (TUFHelperReplayAPI.IsOpeningReplayLevel) return;
    if (!ReplayService.HasActiveContext) return;

    ReplayService.StopActiveReplay(
      "TUFHelper loaded a level outside replay API. levelId=" +
      (levelInfo == null ? "none" : levelInfo.ID.ToString())
    );
  }

  public static bool ControllerUpdateInputPrefix(scrController __instance)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session == null || !session.UsesAsyncInputPlayback || !session.CanInjectInput()) return true;

    session.EnqueueDueAsyncInputEvents();
    return true;
  }

  public static void ValidInputWasTriggeredPostfix(ref bool __result)
  {
    if (__result) return;

    ReplaySession session = Replay.Instance?.Session;
    if (session == null || !session.CanInjectInput()) return;

    __result = session.GetMainCount(ButtonState.WentDown) > 0;
  }

  public static bool CountValidKeysPressedPrefix(ref int __result)
  {
    ReplaySession session = Replay.Instance?.Session;
    if (session == null || !session.CanInjectInput()) return true;

    __result = session.GetMainCount(ButtonState.WentDown);
    return false;
  }
}
