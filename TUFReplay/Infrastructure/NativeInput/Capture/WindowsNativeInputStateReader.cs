using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class WindowsNativeInputStateReader : INativeInputStateReader
{
  private readonly int[] _keyCodes = BuildKeyCodes();
  private readonly byte[] _keyState = new byte[256];

  public string Name => "windows-keyboard-state";
  public IReadOnlyList<int> KeyCodes => _keyCodes;

  [DllImport("user32.dll")]
  private static extern int GetKeyboardState(byte[] keyState);

  public void Refresh()
  {
    GetKeyboardState(_keyState);
  }

  public bool TryGetIsDown(int keyCode, out bool isDown)
  {
    isDown = false;
    if (keyCode <= 0 || keyCode >= _keyState.Length)
      return false;

    isDown = (_keyState[keyCode] & 0x80) != 0;
    return true;
  }

  private static int[] BuildKeyCodes()
  {
    List<int> keyCodes = new List<int>();
    for (int i = 1; i < 256; i++)
    {
      keyCodes.Add(i);
    }

    return keyCodes.ToArray();
  }
}
