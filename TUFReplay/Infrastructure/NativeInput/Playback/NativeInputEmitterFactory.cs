using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput;

public static class NativeInputEmitterFactory
{
  public static INativeInputEmitter Create()
  {
    return Create(NativeInputFocusGuardFactory.Create());
  }

  internal static INativeInputEmitter Create(INativeInputFocusGuard focusGuard)
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return new MacOsNativeInputEmitter();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return new WindowsNativeInputEmitter(focusGuard);

    return new NoopNativeInputEmitter();
  }
}
