using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TUFReplay.Infrastructure.NativeInput;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class WindowsNativeInputStateReader : INativeInputStateReader
{
  private readonly int[] _keyCodes = BuildKeyCodes();
  private readonly byte[] _keyState = new byte[256];

  public string Name => "windows-keyboard-state";
  public IReadOnlyList<int> KeyCodes => _keyCodes;

  [DllImport("user32.dll", SetLastError = true)]
  private static extern int GetKeyboardState(byte[] keyState);

  public void Refresh()
  {
    if (GetKeyboardState(_keyState) == 0)
      throw new Win32Exception(Marshal.GetLastWin32Error());
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
      if (WindowsNativeInputKey.IsMouseButton(i) || i == 0x10 || i == 0x11 || i == 0x12)
        continue;
      keyCodes.Add(i);
    }

    return keyCodes.ToArray();
  }
}
