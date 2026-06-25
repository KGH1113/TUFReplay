using System.Runtime.InteropServices;

namespace TUFReplay.Replay.NativeInput;

public static class NativeInputEmitterFactory
{
  public static INativeInputEmitter Create()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return new MacOsNativeInputEmitter();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsNativeInputEmitter();

    return new NoopNativeInputEmitter();
  }
}
