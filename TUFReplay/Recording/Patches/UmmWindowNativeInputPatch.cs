using System;
using System.Reflection;
using HarmonyLib;
using TUFReplay.Recording.Input;
using TUFReplay.Recording.Sessions;
using TUFReplay.Replay.Sessions;
using TUFReplay.Replay.Transport;
using TUFReplay.Shared.NativeInput;
using UnityModManagerNet;

namespace TUFReplay.Recording.Patches;

[HarmonyPatch]
internal static class UmmWindowNativeInputPatch
{
  private static readonly MethodBase ToggleWindowMethod = ResolveTargetMethod();

  [HarmonyPrepare]
  private static bool Prepare()
  {
    bool available = ToggleWindowMethod != null;
    NativeInputUmmWindowInterlock.ConfigureManagerWindowPatch(available);
    return available;
  }

  private static MethodBase TargetMethod()
  {
    return ToggleWindowMethod;
  }

  private static MethodBase ResolveTargetMethod()
  {
    Type uiType = typeof(UnityModManager).GetNestedType("UI", BindingFlags.Public | BindingFlags.NonPublic);
    return uiType?.GetMethod(
      "ToggleWindow",
      BindingFlags.Public | BindingFlags.Instance,
      null,
      new[] { typeof(bool) },
      null
    );
  }

  [HarmonyPrefix]
  private static void Prefix(bool open)
  {
    NativeInputUmmWindowInterlock.NotifyWindowOpen(open);
    if (!open)
    {
      ReplaySessionService.ResumeNativeInputAfterUmmWindow();
      return;
    }

    try
    {
      RecordingSession session = RecordingFeature.Instance?.Session;
      if (session != null)
        RecordInputTracker.DrainCapturedTransitions(session);
      session?.BreakInputTimeline("umm_window");
      RecordInputTracker.SetCaptureWindowActive(false);
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("UMM native-input capture suspension", exception);
    }

    try
    {
      ReplaySessionService.SuspendNativeInputForUmmWindow();
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("UMM native-input replay suspension", exception);
    }
  }
}
