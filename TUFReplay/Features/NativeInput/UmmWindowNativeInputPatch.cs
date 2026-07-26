using System;
using System.Reflection;
using HarmonyLib;
using TUFReplay.Application.Recording;
using TUFReplay.Application.Replay;
using TUFReplay.Infrastructure.NativeInput;
using UnityModManagerNet;

namespace TUFReplay.Features.NativeInput;

[HarmonyPatch]
internal static class UmmWindowNativeInputPatch
{
  [HarmonyPrepare]
  private static bool Prepare()
  {
    return ResolveTargetMethod() != null;
  }

  private static MethodBase TargetMethod()
  {
    return ResolveTargetMethod();
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
      return;

    try
    {
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

  [HarmonyPostfix]
  private static void Postfix()
  {
    NativeInputUmmWindowInterlock.SynchronizeWithManagerWindow();
  }
}
