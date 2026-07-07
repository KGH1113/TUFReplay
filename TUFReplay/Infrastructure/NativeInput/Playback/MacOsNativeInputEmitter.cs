using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput;

public sealed class MacOsNativeInputEmitter : INativeInputEmitter
{
  private const int EscapeKeyCode = 0x35;
  private CGEventFlags _modifierFlags;

  private enum CGEventTapLocation
  {
    HID = 0
  }

  [Flags]
  private enum CGEventFlags : ulong
  {
    None = 0,
    MaskAlphaShift = 0x00010000,
    MaskShift = 0x00020000,
    MaskControl = 0x00040000,
    MaskAlternate = 0x00080000,
    MaskCommand = 0x00100000
  }

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern IntPtr CGEventCreateKeyboardEvent(
    IntPtr source,
    ushort virtualKey,
    [MarshalAs(UnmanagedType.I1)] bool keyDown
  );

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern void CGEventSetFlags(IntPtr eventRef, CGEventFlags flags);

  [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
  private static extern void CGEventPost(CGEventTapLocation tap, IntPtr eventRef);

  [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
  private static extern void CFRelease(IntPtr cf);

  public bool Emit(int key, bool down)
  {
    if (key < 0 || key > 0x7F) return false;
    if (IsBlockedKey(key)) return false;

    CGEventFlags nextFlags = GetNextModifierFlags(key, down);
    IntPtr ev = CGEventCreateKeyboardEvent(IntPtr.Zero, (ushort)key, down);
    if (ev == IntPtr.Zero) return false;

    try
    {
      CGEventSetFlags(ev, nextFlags);
      CGEventPost(CGEventTapLocation.HID, ev);
      _modifierFlags = nextFlags;
      return true;
    }
    finally
    {
      CFRelease(ev);
    }
  }

  private static bool IsBlockedKey(int key)
  {
    return key == EscapeKeyCode;
  }

  private CGEventFlags GetNextModifierFlags(int key, bool down)
  {
    if (!TryGetModifierFlag(key, out CGEventFlags flag))
    {
      return _modifierFlags;
    }

    return down ? _modifierFlags | flag : _modifierFlags & ~flag;
  }

  private static bool TryGetModifierFlag(int key, out CGEventFlags flag)
  {
    switch (key)
    {
      case 0x39:
        flag = CGEventFlags.MaskAlphaShift;
        return true;

      case 0x38:
      case 0x3C:
        flag = CGEventFlags.MaskShift;
        return true;

      case 0x3B:
      case 0x3E:
        flag = CGEventFlags.MaskControl;
        return true;

      case 0x3A:
      case 0x3D:
        flag = CGEventFlags.MaskAlternate;
        return true;

      case 0x36:
      case 0x37:
        flag = CGEventFlags.MaskCommand;
        return true;

      default:
        flag = CGEventFlags.None;
        return false;
    }
  }
}
