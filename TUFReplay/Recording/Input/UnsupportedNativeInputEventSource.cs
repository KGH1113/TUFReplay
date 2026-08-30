using System;
using System.Collections.Generic;

namespace TUFReplay.Recording.Input;

internal sealed class UnsupportedNativeInputEventSource : INativeInputEventSource
{
  private static readonly int[] NoKeys = Array.Empty<int>();

  public string Name => "unsupported-native-input";
  public bool IsRunning => false;
  public bool UsesExtendedKeyState => false;
  public IReadOnlyList<int> SnapshotKeyCodes => NoKeys;

  public void Start(Action<NativeInputTransition> onTransition)
  {
    throw new PlatformNotSupportedException(
      "High-resolution input recording currently requires Windows WH_KEYBOARD_LL. macOS IOHID support is not implemented."
    );
  }

  public void Stop() { }

  public void RefreshPhysicalState() { }

  public bool TryGetPhysicalKeyState(int keyCode, out bool isDown)
  {
    isDown = false;
    return false;
  }

  public long ConsumeDroppedEvents() => 0;

  public NativeInputSourceDiagnostics GetDiagnostics() => default;
}
