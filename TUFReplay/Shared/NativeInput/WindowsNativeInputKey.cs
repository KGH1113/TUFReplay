using SkyHook;

namespace TUFReplay.Shared.NativeInput;

internal static class WindowsNativeInputKey
{
  public static bool IsMouseButton(int virtualKey)
  {
    return virtualKey >= 0x01 && virtualKey <= 0x06;
  }

  public static int NormalizeCapturedVirtualKey(int virtualKey, KeyLabel label)
  {
    switch (virtualKey)
    {
      case 0x10: // VK_SHIFT
        if (label == KeyLabel.LShift)
          return 0xA0;
        if (label == KeyLabel.RShift)
          return 0xA1;
        break;

      case 0x11: // VK_CONTROL
        if (label == KeyLabel.LControl)
          return 0xA2;
        if (label == KeyLabel.RControl)
          return 0xA3;
        break;

      case 0x12: // VK_MENU
        if (label == KeyLabel.LAlt)
          return 0xA4;
        if (label == KeyLabel.RAlt)
          return 0xA5;
        break;
    }

    return virtualKey;
  }

  public static bool IsExtended(int virtualKey, KeyLabel label)
  {
    return label == KeyLabel.KeypadEnter || IsExtended(virtualKey);
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

  public static bool IsExtendedLabel(KeyLabel label)
  {
    switch (label)
    {
      case KeyLabel.RControl:
      case KeyLabel.RAlt:
      case KeyLabel.Super:
      case KeyLabel.PrintScreen:
      case KeyLabel.Insert:
      case KeyLabel.Delete:
      case KeyLabel.Home:
      case KeyLabel.End:
      case KeyLabel.PageUp:
      case KeyLabel.PageDown:
      case KeyLabel.ArrowLeft:
      case KeyLabel.ArrowRight:
      case KeyLabel.ArrowUp:
      case KeyLabel.ArrowDown:
      case KeyLabel.KeypadSlash:
      case KeyLabel.KeypadEnter:
        return true;
      default:
        return false;
    }
  }
}
