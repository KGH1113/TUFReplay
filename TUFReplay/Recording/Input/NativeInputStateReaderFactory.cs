using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal static class NativeInputStateReaderFactory
{
  public static INativeInputStateReader Create()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      return new MacOsNativeInputStateReader();
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return new WindowsNativeInputStateReader();
    }

    return new NoopNativeInputStateReader();
  }
}
