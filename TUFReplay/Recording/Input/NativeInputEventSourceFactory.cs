using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal static class NativeInputEventSourceFactory
{
  public static INativeInputEventSource CreatePrimary()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      if (WindowsNativeKeyboardEventSource.TryCreate(out INativeInputEventSource native, out string failure))
        return native;
      // Without the native library, keep recording through the managed hook.
      Main.Instance?.Log("[Recording/Input] Using the managed keyboard hook. reason=" + failure);
      return new WindowsLowLevelKeyboardEventSource();
    }
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return new MacOsCGEventInputEventSource();
    return new UnsupportedNativeInputEventSource();
  }
}
