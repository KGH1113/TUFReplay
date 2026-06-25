using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TUFReplay.Recording.NativeInput;

internal interface INativeInputStateReader
{
  string Name { get; }
  IReadOnlyList<int> KeyCodes { get; }
  void Refresh();
  bool TryGetIsDown(int keyCode, out bool isDown);
}

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

internal sealed class MacOsNativeInputStateReader : INativeInputStateReader
{
  private readonly int[] _keyCodes = BuildKeyCodes(0, 0x7F);

  public string Name => "macos-native-key-state";
  public IReadOnlyList<int> KeyCodes => _keyCodes;

  public void Refresh()
  {
  }

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  [return: MarshalAs(UnmanagedType.I1)]
  private static extern bool CGEventSourceKeyState(int stateID, ushort virtualKey);

  public bool TryGetIsDown(int keyCode, out bool isDown)
  {
    isDown = false;
    if (keyCode < 0 || keyCode > 0x7F) return false;

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
    if (keyCode <= 0 || keyCode >= _keyState.Length) return false;

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

internal sealed class NoopNativeInputStateReader : INativeInputStateReader
{
  private static readonly int[] NoKeyCodes = new int[0];

  public string Name => "unsupported";
  public IReadOnlyList<int> KeyCodes => NoKeyCodes;

  public void Refresh()
  {
  }

  public bool TryGetIsDown(int keyCode, out bool isDown)
  {
    isDown = false;
    return false;
  }
}
