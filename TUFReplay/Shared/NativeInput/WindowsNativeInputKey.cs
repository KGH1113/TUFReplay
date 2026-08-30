
namespace TUFReplay.Shared.NativeInput;

internal static class WindowsNativeInputKey
{
  private const int RightShiftVirtualKey = 0xA1;
  private const int RightShiftScanCode = 0x36;

  public static bool IsMouseButton(int virtualKey)
  {
    return virtualKey >= 0x01 && virtualKey <= 0x06;
  }

  public static bool IsExtended(int virtualKey)
  {
    switch (virtualKey)
    {
      case 0x03: // VK_CANCEL / Break
      case 0x21: // Page Up
      case 0x22: // Page Down
      case 0x23: // End
      case 0x24: // Home
      case 0x25: // Left
      case 0x26: // Up
      case 0x27: // Right
      case 0x28: // Down
      case 0x2C: // Print Screen
      case 0x2D: // Insert
      case 0x2E: // Delete
      case 0x5B: // Left Windows
      case 0x5C: // Right Windows
      case 0x5D: // Application/Menu
      case 0x6F: // Numpad Divide
      case 0xA3: // Right Ctrl
      case 0xA5: // Right Alt
        return true;
      default:
        return false;
    }
  }

  public static bool NormalizeExtended(int virtualKey, int scanCode, bool reportedExtended)
  {
    // Right Shift has its own scan code, but it is not an E0 extended key.
    // Some low-level hooks report LLKHF_EXTENDED for it; preserve that raw
    // provenance separately, never translate it to KEYEVENTF_EXTENDEDKEY.
    if (virtualKey == RightShiftVirtualKey || scanCode == RightShiftScanCode)
      return false;
    return reportedExtended;
  }

  public static bool IsExtendedLabel(LogicalKeyboardKey label)
  {
    switch (label)
    {
      case LogicalKeyboardKey.RControl:
      case LogicalKeyboardKey.RAlt:
      case LogicalKeyboardKey.Super:
      case LogicalKeyboardKey.RSuper:
      case LogicalKeyboardKey.PrintScreen:
      case LogicalKeyboardKey.Insert:
      case LogicalKeyboardKey.Delete:
      case LogicalKeyboardKey.Home:
      case LogicalKeyboardKey.End:
      case LogicalKeyboardKey.PageUp:
      case LogicalKeyboardKey.PageDown:
      case LogicalKeyboardKey.ArrowLeft:
      case LogicalKeyboardKey.ArrowRight:
      case LogicalKeyboardKey.ArrowUp:
      case LogicalKeyboardKey.ArrowDown:
      case LogicalKeyboardKey.KeypadSlash:
      case LogicalKeyboardKey.KeypadEnter:
        return true;
      default:
        return false;
    }
  }
}
