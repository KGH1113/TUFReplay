using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

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
