using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal static class NativeInputEventSourceFactory
{
  public static INativeInputEventSource CreatePrimary()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsLowLevelKeyboardEventSource();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      // The additional Quartz event tap caused progressive system-wide input
      // latency on real macOS gameplay workloads. Keep the conductor-anchor
      // timeline mapper, but use the already-established SkyHook raw callback
      // until the macOS native source is replaced with an IOHID-based capture.
      return new SkyHookNativeInputEventSource();
    return new SkyHookNativeInputEventSource();
  }

  public static INativeInputEventSource CreateFallback() => new SkyHookNativeInputEventSource();
}
