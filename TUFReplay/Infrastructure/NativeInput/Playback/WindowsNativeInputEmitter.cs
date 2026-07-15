using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput;

public sealed class WindowsNativeInputEmitter : INativeInputEmitter
{
  private const uint KeyEventExtendedKey = 0x0001;
  private const uint KeyEventKeyUp = 0x0002;

  [DllImport("user32.dll")]
  private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

  public bool Emit(int key, bool down)
  {
    if (key <= 0 || key > 255)
      return false;
    if (IsBlockedKey((ushort)key))
      return false;

    uint flags = down ? 0u : KeyEventKeyUp;
    if (IsExtendedKey((ushort)key))
      flags |= KeyEventExtendedKey;

    keybd_event((byte)key, 0, flags, UIntPtr.Zero);
    return true;
  }

  private static bool IsBlockedKey(ushort virtualKey)
  {
    switch (virtualKey)
    {
      case 16: // Generic Shift
      case 17: // Generic Ctrl
      case 18: // Generic Alt
      case 27: // Escape
        return true;
      default:
        return false;
    }
  }

  private static bool IsExtendedKey(ushort virtualKey)
  {
    switch (virtualKey)
    {
      case 0x21: // Page Up
      case 0x22: // Page Down
      case 0x23: // End
      case 0x24: // Home
      case 0x25: // Left
      case 0x26: // Up
      case 0x27: // Right
      case 0x28: // Down
      case 0x2D: // Insert
      case 0x2E: // Delete
      case 0x5B: // Left Windows
      case 0x5C: // Right Windows
      case 0x6F: // Numpad Divide
      case 0xA3: // Right Ctrl
      case 0xA5: // Right Alt
        return true;
      default:
        return false;
    }
  }
}
