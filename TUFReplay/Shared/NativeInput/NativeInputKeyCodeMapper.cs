using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TUFReplay.Replay.Models;

namespace TUFReplay.Shared.NativeInput;

internal static class NativeInputKeyCodeMapper
{
  public const string NativeKeySpace = "os-native-key-code";
  private static readonly Dictionary<LogicalKeyboardKey, ushort> MacVirtualKeyCodes = new Dictionary<
    LogicalKeyboardKey,
    ushort
  >
  {
    { LogicalKeyboardKey.A, 0x00 },
    { LogicalKeyboardKey.S, 0x01 },
    { LogicalKeyboardKey.D, 0x02 },
    { LogicalKeyboardKey.F, 0x03 },
    { LogicalKeyboardKey.H, 0x04 },
    { LogicalKeyboardKey.G, 0x05 },
    { LogicalKeyboardKey.Z, 0x06 },
    { LogicalKeyboardKey.X, 0x07 },
    { LogicalKeyboardKey.C, 0x08 },
    { LogicalKeyboardKey.V, 0x09 },
    { LogicalKeyboardKey.B, 0x0B },
    { LogicalKeyboardKey.Q, 0x0C },
    { LogicalKeyboardKey.W, 0x0D },
    { LogicalKeyboardKey.E, 0x0E },
    { LogicalKeyboardKey.R, 0x0F },
    { LogicalKeyboardKey.Y, 0x10 },
    { LogicalKeyboardKey.T, 0x11 },
    { LogicalKeyboardKey.Alpha1, 0x12 },
    { LogicalKeyboardKey.Alpha2, 0x13 },
    { LogicalKeyboardKey.Alpha3, 0x14 },
    { LogicalKeyboardKey.Alpha4, 0x15 },
    { LogicalKeyboardKey.Alpha6, 0x16 },
    { LogicalKeyboardKey.Alpha5, 0x17 },
    { LogicalKeyboardKey.Equal, 0x18 },
    { LogicalKeyboardKey.Alpha9, 0x19 },
    { LogicalKeyboardKey.Alpha7, 0x1A },
    { LogicalKeyboardKey.Minus, 0x1B },
    { LogicalKeyboardKey.Alpha8, 0x1C },
    { LogicalKeyboardKey.Alpha0, 0x1D },
    { LogicalKeyboardKey.RightBrace, 0x1E },
    { LogicalKeyboardKey.O, 0x1F },
    { LogicalKeyboardKey.U, 0x20 },
    { LogicalKeyboardKey.LeftBrace, 0x21 },
    { LogicalKeyboardKey.I, 0x22 },
    { LogicalKeyboardKey.P, 0x23 },
    { LogicalKeyboardKey.Enter, 0x24 },
    { LogicalKeyboardKey.L, 0x25 },
    { LogicalKeyboardKey.J, 0x26 },
    { LogicalKeyboardKey.Apostrophe, 0x27 },
    { LogicalKeyboardKey.K, 0x28 },
    { LogicalKeyboardKey.Semicolon, 0x29 },
    { LogicalKeyboardKey.BackSlash, 0x2A },
    { LogicalKeyboardKey.Comma, 0x2B },
    { LogicalKeyboardKey.Slash, 0x2C },
    { LogicalKeyboardKey.N, 0x2D },
    { LogicalKeyboardKey.M, 0x2E },
    { LogicalKeyboardKey.Dot, 0x2F },
    { LogicalKeyboardKey.Tab, 0x30 },
    { LogicalKeyboardKey.Space, 0x31 },
    { LogicalKeyboardKey.Grave, 0x32 },
    { LogicalKeyboardKey.Backspace, 0x33 },
    { LogicalKeyboardKey.Escape, 0x35 },
    { LogicalKeyboardKey.RSuper, 0x36 },
    { LogicalKeyboardKey.Super, 0x37 },
    { LogicalKeyboardKey.LShift, 0x38 },
    { LogicalKeyboardKey.CapsLock, 0x39 },
    { LogicalKeyboardKey.LAlt, 0x3A },
    { LogicalKeyboardKey.LControl, 0x3B },
    { LogicalKeyboardKey.RShift, 0x3C },
    { LogicalKeyboardKey.RAlt, 0x3D },
    { LogicalKeyboardKey.RControl, 0x3E },
    { LogicalKeyboardKey.F17, 0x40 },
    { LogicalKeyboardKey.KeypadDot, 0x41 },
    { LogicalKeyboardKey.KeypadAsterisk, 0x43 },
    { LogicalKeyboardKey.KeypadPlus, 0x45 },
    { LogicalKeyboardKey.KeypadSlash, 0x4B },
    { LogicalKeyboardKey.KeypadEnter, 0x4C },
    { LogicalKeyboardKey.KeypadMinus, 0x4E },
    { LogicalKeyboardKey.F18, 0x4F },
    { LogicalKeyboardKey.F19, 0x50 },
    { LogicalKeyboardKey.Keypad0, 0x52 },
    { LogicalKeyboardKey.Keypad1, 0x53 },
    { LogicalKeyboardKey.Keypad2, 0x54 },
    { LogicalKeyboardKey.Keypad3, 0x55 },
    { LogicalKeyboardKey.Keypad4, 0x56 },
    { LogicalKeyboardKey.Keypad5, 0x57 },
    { LogicalKeyboardKey.Keypad6, 0x58 },
    { LogicalKeyboardKey.Keypad7, 0x59 },
    { LogicalKeyboardKey.F20, 0x5A },
    { LogicalKeyboardKey.Keypad8, 0x5B },
    { LogicalKeyboardKey.Keypad9, 0x5C },
    { LogicalKeyboardKey.F5, 0x60 },
    { LogicalKeyboardKey.F6, 0x61 },
    { LogicalKeyboardKey.F7, 0x62 },
    { LogicalKeyboardKey.F3, 0x63 },
    { LogicalKeyboardKey.F8, 0x64 },
    { LogicalKeyboardKey.F9, 0x65 },
    { LogicalKeyboardKey.F11, 0x67 },
    { LogicalKeyboardKey.F13, 0x69 },
    { LogicalKeyboardKey.F16, 0x6A },
    { LogicalKeyboardKey.F14, 0x6B },
    { LogicalKeyboardKey.F10, 0x6D },
    { LogicalKeyboardKey.F12, 0x6F },
    { LogicalKeyboardKey.F15, 0x71 },
    { LogicalKeyboardKey.Insert, 0x72 },
    { LogicalKeyboardKey.Home, 0x73 },
    { LogicalKeyboardKey.PageUp, 0x74 },
    { LogicalKeyboardKey.Delete, 0x75 },
    { LogicalKeyboardKey.End, 0x77 },
    { LogicalKeyboardKey.F2, 0x78 },
    { LogicalKeyboardKey.PageDown, 0x79 },
    { LogicalKeyboardKey.F1, 0x7A },
    { LogicalKeyboardKey.ArrowLeft, 0x7B },
    { LogicalKeyboardKey.ArrowRight, 0x7C },
    { LogicalKeyboardKey.ArrowDown, 0x7D },
    { LogicalKeyboardKey.ArrowUp, 0x7E },
  };
  private static readonly Dictionary<ushort, LogicalKeyboardKey> MacVirtualKeyLabels = CreateReverseMap(
    MacVirtualKeyCodes
  );

  internal static bool TryGetLogicalKeyFromHidUsage(int usage, out LogicalKeyboardKey key)
  {
    if (usage >= 4 && usage <= 29)
    {
      key = (LogicalKeyboardKey)((int)LogicalKeyboardKey.A + usage - 4);
      return true;
    }
    if (usage >= 30 && usage <= 38)
    {
      key = (LogicalKeyboardKey)((int)LogicalKeyboardKey.Alpha1 + usage - 30);
      return true;
    }
    if (usage == 39)
    {
      key = LogicalKeyboardKey.Alpha0;
      return true;
    }
    if (usage >= 58 && usage <= 69)
    {
      key = (LogicalKeyboardKey)((int)LogicalKeyboardKey.F1 + usage - 58);
      return true;
    }
    if (usage >= 89 && usage <= 97)
    {
      key = (LogicalKeyboardKey)((int)LogicalKeyboardKey.Keypad1 + usage - 89);
      return true;
    }
    if (usage >= 104 && usage <= 111)
    {
      key = (LogicalKeyboardKey)((int)LogicalKeyboardKey.F13 + usage - 104);
      return true;
    }

    switch (usage)
    {
      case 40:
        key = LogicalKeyboardKey.Enter;
        return true;
      case 41:
        key = LogicalKeyboardKey.Escape;
        return true;
      case 42:
        key = LogicalKeyboardKey.Backspace;
        return true;
      case 43:
        key = LogicalKeyboardKey.Tab;
        return true;
      case 44:
        key = LogicalKeyboardKey.Space;
        return true;
      case 45:
        key = LogicalKeyboardKey.Minus;
        return true;
      case 46:
        key = LogicalKeyboardKey.Equal;
        return true;
      case 47:
        key = LogicalKeyboardKey.LeftBrace;
        return true;
      case 48:
        key = LogicalKeyboardKey.RightBrace;
        return true;
      case 49:
      case 50:
      case 100:
        key = LogicalKeyboardKey.BackSlash;
        return true;
      case 51:
        key = LogicalKeyboardKey.Semicolon;
        return true;
      case 52:
        key = LogicalKeyboardKey.Apostrophe;
        return true;
      case 53:
        key = LogicalKeyboardKey.Grave;
        return true;
      case 54:
        key = LogicalKeyboardKey.Comma;
        return true;
      case 55:
        key = LogicalKeyboardKey.Dot;
        return true;
      case 56:
        key = LogicalKeyboardKey.Slash;
        return true;
      case 57:
        key = LogicalKeyboardKey.CapsLock;
        return true;
      case 70:
        key = LogicalKeyboardKey.PrintScreen;
        return true;
      case 71:
        key = LogicalKeyboardKey.ScrollLock;
        return true;
      case 72:
        key = LogicalKeyboardKey.PauseBreak;
        return true;
      case 73:
        key = LogicalKeyboardKey.Insert;
        return true;
      case 74:
        key = LogicalKeyboardKey.Home;
        return true;
      case 75:
        key = LogicalKeyboardKey.PageUp;
        return true;
      case 76:
        key = LogicalKeyboardKey.Delete;
        return true;
      case 77:
        key = LogicalKeyboardKey.End;
        return true;
      case 78:
        key = LogicalKeyboardKey.PageDown;
        return true;
      case 79:
        key = LogicalKeyboardKey.ArrowRight;
        return true;
      case 80:
        key = LogicalKeyboardKey.ArrowLeft;
        return true;
      case 81:
        key = LogicalKeyboardKey.ArrowDown;
        return true;
      case 82:
        key = LogicalKeyboardKey.ArrowUp;
        return true;
      case 83:
        key = LogicalKeyboardKey.NumLock;
        return true;
      case 84:
        key = LogicalKeyboardKey.KeypadSlash;
        return true;
      case 85:
        key = LogicalKeyboardKey.KeypadAsterisk;
        return true;
      case 86:
        key = LogicalKeyboardKey.KeypadMinus;
        return true;
      case 87:
        key = LogicalKeyboardKey.KeypadPlus;
        return true;
      case 88:
        key = LogicalKeyboardKey.KeypadEnter;
        return true;
      case 98:
        key = LogicalKeyboardKey.Keypad0;
        return true;
      case 99:
        key = LogicalKeyboardKey.KeypadDot;
        return true;
      case 224:
        key = LogicalKeyboardKey.LControl;
        return true;
      case 225:
        key = LogicalKeyboardKey.LShift;
        return true;
      case 226:
        key = LogicalKeyboardKey.LAlt;
        return true;
      case 227:
        key = LogicalKeyboardKey.Super;
        return true;
      case 228:
        key = LogicalKeyboardKey.RControl;
        return true;
      case 229:
        key = LogicalKeyboardKey.RShift;
        return true;
      case 230:
        key = LogicalKeyboardKey.RAlt;
        return true;
      case 231:
        key = LogicalKeyboardKey.RSuper;
        return true;
      default:
        key = LogicalKeyboardKey.Unknown;
        return false;
    }
  }

  internal static bool TryGetMacVirtualKeyFromHidUsage(int usage, out int virtualKey)
  {
    virtualKey = 0;
    if (
      !TryGetLogicalKeyFromHidUsage(usage, out LogicalKeyboardKey key)
      || !MacVirtualKeyCodes.TryGetValue(key, out ushort mapped)
    )
      return false;
    virtualKey = mapped;
    return true;
  }

  public static List<RecordedInput> NormalizeForPlayback(
    List<RecordedInput> inputs,
    ReplayMetadata meta,
    out int dropped
  )
  {
    dropped = 0;
    if (inputs == null)
      return new List<RecordedInput>();

    string currentPlatform = CurrentPlatform();
    if (
      currentPlatform != "unsupported"
      && !string.Equals(meta.inputNativePlatform, currentPlatform, StringComparison.OrdinalIgnoreCase)
    )
      return ConvertNativePlatform(inputs, meta.inputNativePlatform, out dropped);
    return inputs;
  }

  private static List<RecordedInput> ConvertNativePlatform(
    List<RecordedInput> inputs,
    string sourcePlatform,
    out int dropped
  )
  {
    dropped = 0;
    List<RecordedInput> converted = new List<RecordedInput>(inputs.Count);
    foreach (RecordedInput input in inputs)
    {
      if (
        !TryGetSourceLogicalKey(sourcePlatform, input, out LogicalKeyboardKey label)
        || !TryConvertLogicalKey(label, out int currentKey)
      )
      {
        dropped++;
        continue;
      }

      RecordInputFlags flags = input.Flags & ~RecordInputFlags.ExtendedKey;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsNativeInputKey.IsExtendedLabel(label))
        flags |= RecordInputFlags.ExtendedKey;
      converted.Add(new RecordedInput(input.TimeUs, currentKey, flags));
    }
    return converted;
  }

  private static bool TryGetSourceLogicalKey(string sourcePlatform, RecordedInput input, out LogicalKeyboardKey label)
  {
    label = LogicalKeyboardKey.Unknown;
    int nativeKey = input.Key;
    if (nativeKey < 0 || nativeKey > ushort.MaxValue)
      return false;
    if (string.Equals(sourcePlatform, "macos", StringComparison.OrdinalIgnoreCase))
      return MacVirtualKeyLabels.TryGetValue((ushort)nativeKey, out label);
    if (!string.Equals(sourcePlatform, "windows", StringComparison.OrdinalIgnoreCase))
      return false;
    if (nativeKey == 0x0D && input.ExtendedKey)
    {
      label = LogicalKeyboardKey.KeypadEnter;
      return true;
    }
    return TryGetWindowsKeyLabel(nativeKey, out label);
  }

  private static bool TryGetWindowsKeyLabel(int key, out LogicalKeyboardKey label)
  {
    if (key >= 0x41 && key <= 0x5A)
    {
      LogicalKeyboardKey[] letters =
      {
        LogicalKeyboardKey.A,
        LogicalKeyboardKey.B,
        LogicalKeyboardKey.C,
        LogicalKeyboardKey.D,
        LogicalKeyboardKey.E,
        LogicalKeyboardKey.F,
        LogicalKeyboardKey.G,
        LogicalKeyboardKey.H,
        LogicalKeyboardKey.I,
        LogicalKeyboardKey.J,
        LogicalKeyboardKey.K,
        LogicalKeyboardKey.L,
        LogicalKeyboardKey.M,
        LogicalKeyboardKey.N,
        LogicalKeyboardKey.O,
        LogicalKeyboardKey.P,
        LogicalKeyboardKey.Q,
        LogicalKeyboardKey.R,
        LogicalKeyboardKey.S,
        LogicalKeyboardKey.T,
        LogicalKeyboardKey.U,
        LogicalKeyboardKey.V,
        LogicalKeyboardKey.W,
        LogicalKeyboardKey.X,
        LogicalKeyboardKey.Y,
        LogicalKeyboardKey.Z,
      };
      label = letters[key - 0x41];
      return true;
    }
    if (key >= 0x30 && key <= 0x39)
    {
      LogicalKeyboardKey[] digits =
      {
        LogicalKeyboardKey.Alpha0,
        LogicalKeyboardKey.Alpha1,
        LogicalKeyboardKey.Alpha2,
        LogicalKeyboardKey.Alpha3,
        LogicalKeyboardKey.Alpha4,
        LogicalKeyboardKey.Alpha5,
        LogicalKeyboardKey.Alpha6,
        LogicalKeyboardKey.Alpha7,
        LogicalKeyboardKey.Alpha8,
        LogicalKeyboardKey.Alpha9,
      };
      label = digits[key - 0x30];
      return true;
    }
    if (key >= 0x70 && key <= 0x87)
    {
      LogicalKeyboardKey[] functionKeys =
      {
        LogicalKeyboardKey.F1,
        LogicalKeyboardKey.F2,
        LogicalKeyboardKey.F3,
        LogicalKeyboardKey.F4,
        LogicalKeyboardKey.F5,
        LogicalKeyboardKey.F6,
        LogicalKeyboardKey.F7,
        LogicalKeyboardKey.F8,
        LogicalKeyboardKey.F9,
        LogicalKeyboardKey.F10,
        LogicalKeyboardKey.F11,
        LogicalKeyboardKey.F12,
        LogicalKeyboardKey.F13,
        LogicalKeyboardKey.F14,
        LogicalKeyboardKey.F15,
        LogicalKeyboardKey.F16,
        LogicalKeyboardKey.F17,
        LogicalKeyboardKey.F18,
        LogicalKeyboardKey.F19,
        LogicalKeyboardKey.F20,
        LogicalKeyboardKey.F21,
        LogicalKeyboardKey.F22,
        LogicalKeyboardKey.F23,
        LogicalKeyboardKey.F24,
      };
      label = functionKeys[key - 0x70];
      return true;
    }
    if (key >= 0x60 && key <= 0x69)
    {
      LogicalKeyboardKey[] keypadDigits =
      {
        LogicalKeyboardKey.Keypad0,
        LogicalKeyboardKey.Keypad1,
        LogicalKeyboardKey.Keypad2,
        LogicalKeyboardKey.Keypad3,
        LogicalKeyboardKey.Keypad4,
        LogicalKeyboardKey.Keypad5,
        LogicalKeyboardKey.Keypad6,
        LogicalKeyboardKey.Keypad7,
        LogicalKeyboardKey.Keypad8,
        LogicalKeyboardKey.Keypad9,
      };
      label = keypadDigits[key - 0x60];
      return true;
    }

    switch (key)
    {
      case 0x08:
        label = LogicalKeyboardKey.Backspace;
        return true;
      case 0x09:
        label = LogicalKeyboardKey.Tab;
        return true;
      case 0x0D:
        label = LogicalKeyboardKey.Enter;
        return true;
      case 0x13:
        label = LogicalKeyboardKey.PauseBreak;
        return true;
      case 0x14:
        label = LogicalKeyboardKey.CapsLock;
        return true;
      case 0x1B:
        label = LogicalKeyboardKey.Escape;
        return true;
      case 0x20:
        label = LogicalKeyboardKey.Space;
        return true;
      case 0x21:
        label = LogicalKeyboardKey.PageUp;
        return true;
      case 0x22:
        label = LogicalKeyboardKey.PageDown;
        return true;
      case 0x23:
        label = LogicalKeyboardKey.End;
        return true;
      case 0x24:
        label = LogicalKeyboardKey.Home;
        return true;
      case 0x25:
        label = LogicalKeyboardKey.ArrowLeft;
        return true;
      case 0x26:
        label = LogicalKeyboardKey.ArrowUp;
        return true;
      case 0x27:
        label = LogicalKeyboardKey.ArrowRight;
        return true;
      case 0x28:
        label = LogicalKeyboardKey.ArrowDown;
        return true;
      case 0x2C:
        label = LogicalKeyboardKey.PrintScreen;
        return true;
      case 0x2D:
        label = LogicalKeyboardKey.Insert;
        return true;
      case 0x2E:
        label = LogicalKeyboardKey.Delete;
        return true;
      case 0x5B:
        label = LogicalKeyboardKey.Super;
        return true;
      case 0x5C:
        label = LogicalKeyboardKey.RSuper;
        return true;
      case 0x6A:
        label = LogicalKeyboardKey.KeypadAsterisk;
        return true;
      case 0x6B:
        label = LogicalKeyboardKey.KeypadPlus;
        return true;
      case 0x6D:
        label = LogicalKeyboardKey.KeypadMinus;
        return true;
      case 0x6E:
        label = LogicalKeyboardKey.KeypadDot;
        return true;
      case 0x6F:
        label = LogicalKeyboardKey.KeypadSlash;
        return true;
      case 0x90:
        label = LogicalKeyboardKey.NumLock;
        return true;
      case 0x91:
        label = LogicalKeyboardKey.ScrollLock;
        return true;
      case 0xA0:
        label = LogicalKeyboardKey.LShift;
        return true;
      case 0xA1:
        label = LogicalKeyboardKey.RShift;
        return true;
      case 0xA2:
        label = LogicalKeyboardKey.LControl;
        return true;
      case 0xA3:
        label = LogicalKeyboardKey.RControl;
        return true;
      case 0xA4:
        label = LogicalKeyboardKey.LAlt;
        return true;
      case 0xA5:
        label = LogicalKeyboardKey.RAlt;
        return true;
      case 0xBA:
        label = LogicalKeyboardKey.Semicolon;
        return true;
      case 0xBB:
        label = LogicalKeyboardKey.Equal;
        return true;
      case 0xBC:
        label = LogicalKeyboardKey.Comma;
        return true;
      case 0xBD:
        label = LogicalKeyboardKey.Minus;
        return true;
      case 0xBE:
        label = LogicalKeyboardKey.Dot;
        return true;
      case 0xBF:
        label = LogicalKeyboardKey.Slash;
        return true;
      case 0xC0:
        label = LogicalKeyboardKey.Grave;
        return true;
      case 0xDB:
        label = LogicalKeyboardKey.LeftBrace;
        return true;
      case 0xDC:
        label = LogicalKeyboardKey.BackSlash;
        return true;
      case 0xDD:
        label = LogicalKeyboardKey.RightBrace;
        return true;
      case 0xDE:
        label = LogicalKeyboardKey.Apostrophe;
        return true;
      default:
        label = LogicalKeyboardKey.Unknown;
        return false;
    }
  }

  private static Dictionary<ushort, LogicalKeyboardKey> CreateReverseMap(Dictionary<LogicalKeyboardKey, ushort> source)
  {
    Dictionary<ushort, LogicalKeyboardKey> result = new Dictionary<ushort, LogicalKeyboardKey>();
    foreach (KeyValuePair<LogicalKeyboardKey, ushort> pair in source)
      result[pair.Value] = pair.Key;
    return result;
  }

  private static string CurrentPlatform()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return "macos";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "windows";
    return "unsupported";
  }

  public static bool TryConvertLogicalKey(LogicalKeyboardKey label, out int nativeKeyCode)
  {
    nativeKeyCode = 0;
    if (label == LogicalKeyboardKey.Unknown)
      return false;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      if (MacVirtualKeyCodes.TryGetValue(label, out ushort macKeyCode))
      {
        nativeKeyCode = macKeyCode;
        return true;
      }

      return false;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      if (label == LogicalKeyboardKey.KeypadEnter)
      {
        nativeKeyCode = 0x0D;
        return true;
      }

      for (int virtualKey = 1; virtualKey <= byte.MaxValue; virtualKey++)
      {
        if (TryGetWindowsKeyLabel(virtualKey, out LogicalKeyboardKey candidate) && candidate == label)
        {
          nativeKeyCode = virtualKey;
          return true;
        }
      }
      return false;
    }

    return false;
  }
}
