using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class MacOsNativeInputStateReader : INativeInputStateReader
{
  private readonly int[] _keyCodes = BuildKeyCodes(0, 0x7F);

  public string Name => "macos-native-key-state";
  public IReadOnlyList<int> KeyCodes => _keyCodes;

  public void Refresh() { }

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  [return: MarshalAs(UnmanagedType.I1)]
  private static extern bool CGEventSourceKeyState(int stateID, ushort virtualKey);

  public bool TryGetIsDown(int keyCode, out bool isDown)
  {
    isDown = false;
    if (keyCode < 0 || keyCode > 0x7F)
      return false;

    isDown = CGEventSourceKeyState(0, (ushort)keyCode);
    return true;
  }

  private static int[] BuildKeyCodes(int min, int max)
  {
    List<int> keyCodes = new List<int>();
    for (int i = min; i <= max; i++)
    {
      keyCodes.Add(i);
    }

    return keyCodes.ToArray();
  }
}
