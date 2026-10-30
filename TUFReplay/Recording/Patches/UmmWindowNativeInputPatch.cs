using System;
using System.Reflection;
using HarmonyLib;
using TUFReplay.Composition;
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
    if (open)
      NativeInputSuspension.Suspend("umm_window");
    else
      NativeInputSuspension.Resume();
  }
}
