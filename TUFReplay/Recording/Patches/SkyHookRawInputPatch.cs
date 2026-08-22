using HarmonyLib;
using SkyHook;
using TUFReplay.Recording.Input;

namespace TUFReplay.Recording.Patches;

[HarmonyPatch(typeof(SkyHookManager), "HookCallback")]
internal static class SkyHookRawInputPatch
{
  [HarmonyPrefix]
  [HarmonyPriority(Priority.First)]
  internal static void Prefix(SkyHookEvent ev)
  {
    SkyHookNativeInputEventSource.PublishRawEvent(ev);
  }
}
