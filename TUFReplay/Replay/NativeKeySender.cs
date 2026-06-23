using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TUFReplay.Replay;

public static class NativeKeySender
{
  private static readonly HashSet<KeyCode> HeldKeys = new HashSet<KeyCode>();
  private static bool _loggedUnsupportedPlatform;
  private static bool _loggedSendFailure;
  private static int _sendLogCount;
  private static int _unsupportedKeyLogCount;
  private static int _apiFailureLogCount;

  public static void Send(KeyCode keyCode, bool down)
  {
    try
    {
      if (down)
      {
        if (!HeldKeys.Add(keyCode)) return;
      }
      else if (!HeldKeys.Remove(keyCode))
      {
        return;
      }

      if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
      {
        LogSend("Windows", keyCode, down);
        WindowsKeySender.Send(keyCode, down);
        return;
      }

      if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
      {
        LogSend("macOS", keyCode, down);
        MacKeySender.Send(keyCode, down);
        return;
      }

      if (!_loggedUnsupportedPlatform)
      {
        _loggedUnsupportedPlatform = true;
        Replay.LogDebug("Native key mirror is not implemented on this OS. platform=" + Application.platform);
      }
    }
    catch (Exception e)
    {
      if (_loggedSendFailure) return;

      _loggedSendFailure = true;
      Replay.LogDebug("Native key mirror failed: " + e.GetType().Name + " - " + e.Message);
    }
  }

  public static void ReleaseAll()
  {
    KeyCode[] keys = new KeyCode[HeldKeys.Count];
    HeldKeys.CopyTo(keys);

    if (keys.Length > 0)
    {
      Replay.LogDebug("Native key mirror releasing held keys. count=" + keys.Length);
    }

    foreach (KeyCode keyCode in keys)
    {
      Send(keyCode, false);
    }

    HeldKeys.Clear();
  }

  private static void LogSend(string platform, KeyCode keyCode, bool down)
  {
    _sendLogCount++;
    if (_sendLogCount > 30 && _sendLogCount % 100 != 0) return;

    Replay.LogDebug(
      "Native key mirror dispatch. platform=" +
      platform +
      ", key=" +
      keyCode +
      ", state=" +
      (down ? "down" : "up") +
      ", heldCount=" +
      HeldKeys.Count +
      ", count=" +
      _sendLogCount
    );
  }

  private static void LogUnsupportedKey(string platform, KeyCode keyCode)
  {
    _unsupportedKeyLogCount++;
    if (_unsupportedKeyLogCount > 20 && _unsupportedKeyLogCount % 50 != 0) return;

    Replay.LogDebug(
      "Native key mirror unsupported key. platform=" +
      platform +
      ", key=" +
      keyCode +
      ", count=" +
      _unsupportedKeyLogCount
    );
  }

  private static void LogApiFailure(string message)
  {
    _apiFailureLogCount++;
    if (_apiFailureLogCount > 20 && _apiFailureLogCount % 50 != 0) return;

    Replay.LogDebug("Native key mirror API failure. " + message + ", count=" + _apiFailureLogCount);
  }

  private static class WindowsKeySender
  {
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
      public uint type;
      public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
      public ushort wVk;
      public ushort wScan;
      public uint dwFlags;
      public uint time;
      public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    public static void Send(KeyCode keyCode, bool down)
    {
      if (!TryGetVirtualKey(keyCode, out ushort virtualKey))
      {
        LogUnsupportedKey("Windows", keyCode);
        return;
      }

      Input[] inputs =
      {
        new Input
        {
          type = InputKeyboard,
          ki = new KeyboardInput
          {
            wVk = virtualKey,
            dwFlags = down ? 0u : KeyEventKeyUp
          }
        }
      };

      uint sent = SendInput(1, inputs, Marshal.SizeOf(typeof(Input)));
      if (sent != 1)
      {
        LogApiFailure(
          "platform=Windows, api=SendInput, key=" +
          keyCode +
          ", state=" +
          (down ? "down" : "up") +
          ", error=" +
          Marshal.GetLastWin32Error()
        );
      }
    }

    private static bool TryGetVirtualKey(KeyCode keyCode, out ushort virtualKey)
    {
      if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
      {
        virtualKey = (ushort)('A' + (keyCode - KeyCode.A));
        return true;
      }

      if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
      {
        virtualKey = (ushort)('0' + (keyCode - KeyCode.Alpha0));
        return true;
      }

      if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
      {
        virtualKey = (ushort)(0x60 + (keyCode - KeyCode.Keypad0));
        return true;
      }

      switch (keyCode)
      {
        case KeyCode.Backspace:
          virtualKey = 0x08;
          return true;
        case KeyCode.Tab:
          virtualKey = 0x09;
          return true;
        case KeyCode.Return:
        case KeyCode.KeypadEnter:
          virtualKey = 0x0D;
          return true;
        case KeyCode.LeftShift:
        case KeyCode.RightShift:
          virtualKey = 0x10;
          return true;
        case KeyCode.LeftControl:
        case KeyCode.RightControl:
          virtualKey = 0x11;
          return true;
        case KeyCode.LeftAlt:
        case KeyCode.RightAlt:
          virtualKey = 0x12;
          return true;
        case KeyCode.Escape:
          virtualKey = 0x1B;
          return true;
        case KeyCode.Space:
          virtualKey = 0x20;
          return true;
        case KeyCode.LeftArrow:
          virtualKey = 0x25;
          return true;
        case KeyCode.UpArrow:
          virtualKey = 0x26;
          return true;
        case KeyCode.RightArrow:
          virtualKey = 0x27;
          return true;
        case KeyCode.DownArrow:
          virtualKey = 0x28;
          return true;
        case KeyCode.Delete:
          virtualKey = 0x2E;
          return true;
        case KeyCode.KeypadMultiply:
          virtualKey = 0x6A;
          return true;
        case KeyCode.KeypadPlus:
          virtualKey = 0x6B;
          return true;
        case KeyCode.KeypadMinus:
          virtualKey = 0x6D;
          return true;
        case KeyCode.KeypadPeriod:
          virtualKey = 0x6E;
          return true;
        case KeyCode.KeypadDivide:
          virtualKey = 0x6F;
          return true;
        case KeyCode.Semicolon:
          virtualKey = 0xBA;
          return true;
        case KeyCode.Equals:
          virtualKey = 0xBB;
          return true;
        case KeyCode.Comma:
          virtualKey = 0xBC;
          return true;
        case KeyCode.Minus:
          virtualKey = 0xBD;
          return true;
        case KeyCode.Period:
          virtualKey = 0xBE;
          return true;
        case KeyCode.Slash:
          virtualKey = 0xBF;
          return true;
        case KeyCode.BackQuote:
          virtualKey = 0xC0;
          return true;
        case KeyCode.LeftBracket:
          virtualKey = 0xDB;
          return true;
        case KeyCode.Backslash:
          virtualKey = 0xDC;
          return true;
        case KeyCode.RightBracket:
          virtualKey = 0xDD;
          return true;
        case KeyCode.Quote:
          virtualKey = 0xDE;
          return true;
      }

      virtualKey = 0;
      return false;
    }
  }

  private static class MacKeySender
  {
    private const uint HidEventTap = 0;

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventPost(uint tap, IntPtr eventRef);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGEventSetFlags(IntPtr eventRef, ulong flags);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    public static void Send(KeyCode keyCode, bool down)
    {
      if (!TryGetVirtualKey(keyCode, out ushort virtualKey))
      {
        LogUnsupportedKey("macOS", keyCode);
        return;
      }

      IntPtr ev = CGEventCreateKeyboardEvent(IntPtr.Zero, virtualKey, down);
      if (ev == IntPtr.Zero)
      {
        LogApiFailure(
          "platform=macOS, api=CGEventCreateKeyboardEvent, key=" +
          keyCode +
          ", virtualKey=" +
          virtualKey +
          ", state=" +
          (down ? "down" : "up")
        );
        return;
      }

      CGEventSetFlags(ev, GetCurrentModifierFlags());
      CGEventPost(HidEventTap, ev);
      CFRelease(ev);
    }

    private static ulong GetCurrentModifierFlags()
    {
      ulong flags = 0;
      if (HeldKeys.Contains(KeyCode.LeftShift) || HeldKeys.Contains(KeyCode.RightShift)) flags |= 0x00020000;
      if (HeldKeys.Contains(KeyCode.LeftControl) || HeldKeys.Contains(KeyCode.RightControl)) flags |= 0x00040000;
      if (HeldKeys.Contains(KeyCode.LeftAlt) || HeldKeys.Contains(KeyCode.RightAlt)) flags |= 0x00080000;
      if (HeldKeys.Contains(KeyCode.LeftCommand) || HeldKeys.Contains(KeyCode.RightCommand)) flags |= 0x00100000;
      return flags;
    }

    private static bool TryGetVirtualKey(KeyCode keyCode, out ushort virtualKey)
    {
      if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
      {
        ushort[] functionKeys = { 122, 120, 99, 118, 96, 97, 98, 100, 101, 109, 103, 111 };
        virtualKey = functionKeys[keyCode - KeyCode.F1];
        return true;
      }

      switch (keyCode)
      {
        case KeyCode.A:
          virtualKey = 0;
          return true;
        case KeyCode.S:
          virtualKey = 1;
          return true;
        case KeyCode.D:
          virtualKey = 2;
          return true;
        case KeyCode.F:
          virtualKey = 3;
          return true;
        case KeyCode.H:
          virtualKey = 4;
          return true;
        case KeyCode.G:
          virtualKey = 5;
          return true;
        case KeyCode.Z:
          virtualKey = 6;
          return true;
        case KeyCode.X:
          virtualKey = 7;
          return true;
        case KeyCode.C:
          virtualKey = 8;
          return true;
        case KeyCode.V:
          virtualKey = 9;
          return true;
        case KeyCode.B:
          virtualKey = 11;
          return true;
        case KeyCode.Q:
          virtualKey = 12;
          return true;
        case KeyCode.W:
          virtualKey = 13;
          return true;
        case KeyCode.E:
          virtualKey = 14;
          return true;
        case KeyCode.R:
          virtualKey = 15;
          return true;
        case KeyCode.Y:
          virtualKey = 16;
          return true;
        case KeyCode.T:
          virtualKey = 17;
          return true;
        case KeyCode.Alpha1:
          virtualKey = 18;
          return true;
        case KeyCode.Alpha2:
          virtualKey = 19;
          return true;
        case KeyCode.Alpha3:
          virtualKey = 20;
          return true;
        case KeyCode.Alpha4:
          virtualKey = 21;
          return true;
        case KeyCode.Alpha6:
          virtualKey = 22;
          return true;
        case KeyCode.Alpha5:
          virtualKey = 23;
          return true;
        case KeyCode.Equals:
          virtualKey = 24;
          return true;
        case KeyCode.Alpha9:
          virtualKey = 25;
          return true;
        case KeyCode.Alpha7:
          virtualKey = 26;
          return true;
        case KeyCode.Minus:
          virtualKey = 27;
          return true;
        case KeyCode.Alpha8:
          virtualKey = 28;
          return true;
        case KeyCode.Alpha0:
          virtualKey = 29;
          return true;
        case KeyCode.RightBracket:
          virtualKey = 30;
          return true;
        case KeyCode.O:
          virtualKey = 31;
          return true;
        case KeyCode.U:
          virtualKey = 32;
          return true;
        case KeyCode.LeftBracket:
          virtualKey = 33;
          return true;
        case KeyCode.I:
          virtualKey = 34;
          return true;
        case KeyCode.P:
          virtualKey = 35;
          return true;
        case KeyCode.Return:
          virtualKey = 36;
          return true;
        case KeyCode.L:
          virtualKey = 37;
          return true;
        case KeyCode.J:
          virtualKey = 38;
          return true;
        case KeyCode.Quote:
          virtualKey = 39;
          return true;
        case KeyCode.K:
          virtualKey = 40;
          return true;
        case KeyCode.Semicolon:
          virtualKey = 41;
          return true;
        case KeyCode.Backslash:
          virtualKey = 42;
          return true;
        case KeyCode.Comma:
          virtualKey = 43;
          return true;
        case KeyCode.Slash:
          virtualKey = 44;
          return true;
        case KeyCode.N:
          virtualKey = 45;
          return true;
        case KeyCode.M:
          virtualKey = 46;
          return true;
        case KeyCode.Period:
          virtualKey = 47;
          return true;
        case KeyCode.Tab:
          virtualKey = 48;
          return true;
        case KeyCode.Space:
          virtualKey = 49;
          return true;
        case KeyCode.BackQuote:
          virtualKey = 50;
          return true;
        case KeyCode.Backspace:
          virtualKey = 51;
          return true;
        case KeyCode.Escape:
          virtualKey = 53;
          return true;
        case KeyCode.LeftShift:
          virtualKey = 56;
          return true;
        case KeyCode.CapsLock:
          virtualKey = 57;
          return true;
        case KeyCode.LeftAlt:
          virtualKey = 58;
          return true;
        case KeyCode.LeftControl:
          virtualKey = 59;
          return true;
        case KeyCode.RightShift:
          virtualKey = 60;
          return true;
        case KeyCode.RightAlt:
          virtualKey = 61;
          return true;
        case KeyCode.RightControl:
          virtualKey = 62;
          return true;
        case KeyCode.F13:
          virtualKey = 105;
          return true;
        case KeyCode.F14:
          virtualKey = 107;
          return true;
        case KeyCode.F15:
          virtualKey = 113;
          return true;
        case KeyCode.Help:
        case KeyCode.Insert:
          virtualKey = 114;
          return true;
        case KeyCode.Home:
          virtualKey = 115;
          return true;
        case KeyCode.PageUp:
          virtualKey = 116;
          return true;
        case KeyCode.Delete:
          virtualKey = 117;
          return true;
        case KeyCode.End:
          virtualKey = 119;
          return true;
        case KeyCode.PageDown:
          virtualKey = 121;
          return true;
        case KeyCode.KeypadPeriod:
          virtualKey = 65;
          return true;
        case KeyCode.KeypadMultiply:
          virtualKey = 67;
          return true;
        case KeyCode.KeypadPlus:
          virtualKey = 69;
          return true;
        case KeyCode.KeypadDivide:
          virtualKey = 75;
          return true;
        case KeyCode.KeypadEnter:
          virtualKey = 76;
          return true;
        case KeyCode.KeypadMinus:
          virtualKey = 78;
          return true;
        case KeyCode.KeypadEquals:
          virtualKey = 81;
          return true;
        case KeyCode.Keypad0:
          virtualKey = 82;
          return true;
        case KeyCode.Keypad1:
          virtualKey = 83;
          return true;
        case KeyCode.Keypad2:
          virtualKey = 84;
          return true;
        case KeyCode.Keypad3:
          virtualKey = 85;
          return true;
        case KeyCode.Keypad4:
          virtualKey = 86;
          return true;
        case KeyCode.Keypad5:
          virtualKey = 87;
          return true;
        case KeyCode.Keypad6:
          virtualKey = 88;
          return true;
        case KeyCode.Keypad7:
          virtualKey = 89;
          return true;
        case KeyCode.Keypad8:
          virtualKey = 91;
          return true;
        case KeyCode.Keypad9:
          virtualKey = 92;
          return true;
        case KeyCode.LeftArrow:
          virtualKey = 123;
          return true;
        case KeyCode.RightArrow:
          virtualKey = 124;
          return true;
        case KeyCode.DownArrow:
          virtualKey = 125;
          return true;
        case KeyCode.UpArrow:
          virtualKey = 126;
          return true;
      }

      virtualKey = 0;
      return false;
    }
  }
}
