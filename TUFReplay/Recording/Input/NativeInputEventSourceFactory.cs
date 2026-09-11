using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal static class NativeInputEventSourceFactory
{
  public static INativeInputEventSource CreatePrimary()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsLowLevelKeyboardEventSource();
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return new MacOsIoHidInputEventSource();
    return new UnsupportedNativeInputEventSource();
  }
}
