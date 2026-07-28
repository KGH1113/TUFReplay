using HarmonyLib;
using SkyHook;
using TUFReplay.Infrastructure.NativeInput.Capture;

namespace TUFReplay.Features.NativeInput;

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
