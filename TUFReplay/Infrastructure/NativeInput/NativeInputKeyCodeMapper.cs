using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkyHook;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Infrastructure.NativeInput;

internal static class NativeInputKeyCodeMapper
{
  public const string NativeKeySpace = "os-native-key-code";

  private static readonly Dictionary<int, KeyLabel> HidUsageLabels = CreateHidUsageLabels();

  private static readonly Dictionary<KeyLabel, ushort> MacVirtualKeyCodes = new Dictionary<KeyLabel, ushort>
  {
    { KeyLabel.A, 0x00 },
    { KeyLabel.S, 0x01 },
    { KeyLabel.D, 0x02 },
    { KeyLabel.F, 0x03 },
    { KeyLabel.H, 0x04 },
    { KeyLabel.G, 0x05 },
    { KeyLabel.Z, 0x06 },
    { KeyLabel.X, 0x07 },
    { KeyLabel.C, 0x08 },
    { KeyLabel.V, 0x09 },
    { KeyLabel.B, 0x0B },
    { KeyLabel.Q, 0x0C },
    { KeyLabel.W, 0x0D },
    { KeyLabel.E, 0x0E },
    { KeyLabel.R, 0x0F },
    { KeyLabel.Y, 0x10 },
    { KeyLabel.T, 0x11 },
    { KeyLabel.Alpha1, 0x12 },
    { KeyLabel.Alpha2, 0x13 },
    { KeyLabel.Alpha3, 0x14 },
    { KeyLabel.Alpha4, 0x15 },
    { KeyLabel.Alpha6, 0x16 },
    { KeyLabel.Alpha5, 0x17 },
    { KeyLabel.Equal, 0x18 },
    { KeyLabel.Alpha9, 0x19 },
    { KeyLabel.Alpha7, 0x1A },
    { KeyLabel.Minus, 0x1B },
    { KeyLabel.Alpha8, 0x1C },
    { KeyLabel.Alpha0, 0x1D },
    { KeyLabel.RightBrace, 0x1E },
    { KeyLabel.O, 0x1F },
    { KeyLabel.U, 0x20 },
    { KeyLabel.LeftBrace, 0x21 },
    { KeyLabel.I, 0x22 },
    { KeyLabel.P, 0x23 },
    { KeyLabel.Enter, 0x24 },
    { KeyLabel.L, 0x25 },
    { KeyLabel.J, 0x26 },
    { KeyLabel.Apostrophe, 0x27 },
    { KeyLabel.K, 0x28 },
    { KeyLabel.Semicolon, 0x29 },
    { KeyLabel.BackSlash, 0x2A },
    { KeyLabel.Comma, 0x2B },
    { KeyLabel.Slash, 0x2C },
    { KeyLabel.N, 0x2D },
    { KeyLabel.M, 0x2E },
    { KeyLabel.Dot, 0x2F },
    { KeyLabel.Tab, 0x30 },
    { KeyLabel.Space, 0x31 },
    { KeyLabel.Grave, 0x32 },
    { KeyLabel.Backspace, 0x33 },
    { KeyLabel.Escape, 0x35 },
    { KeyLabel.Super, 0x37 },
    { KeyLabel.LShift, 0x38 },
    { KeyLabel.CapsLock, 0x39 },
    { KeyLabel.LAlt, 0x3A },
    { KeyLabel.LControl, 0x3B },
    { KeyLabel.RShift, 0x3C },
    { KeyLabel.RAlt, 0x3D },
    { KeyLabel.RControl, 0x3E },
    { KeyLabel.F17, 0x40 },
    { KeyLabel.KeypadDot, 0x41 },
    { KeyLabel.KeypadAsterisk, 0x43 },
    { KeyLabel.KeypadPlus, 0x45 },
    { KeyLabel.KeypadSlash, 0x4B },
    { KeyLabel.KeypadEnter, 0x4C },
    { KeyLabel.KeypadMinus, 0x4E },
    { KeyLabel.F18, 0x4F },
    { KeyLabel.F19, 0x50 },
    { KeyLabel.Keypad0, 0x52 },
    { KeyLabel.Keypad1, 0x53 },
    { KeyLabel.Keypad2, 0x54 },
    { KeyLabel.Keypad3, 0x55 },
    { KeyLabel.Keypad4, 0x56 },
    { KeyLabel.Keypad5, 0x57 },
    { KeyLabel.Keypad6, 0x58 },
    { KeyLabel.Keypad7, 0x59 },
    { KeyLabel.F20, 0x5A },
    { KeyLabel.Keypad8, 0x5B },
    { KeyLabel.Keypad9, 0x5C },
    { KeyLabel.F5, 0x60 },
    { KeyLabel.F6, 0x61 },
    { KeyLabel.F7, 0x62 },
    { KeyLabel.F3, 0x63 },
    { KeyLabel.F8, 0x64 },
    { KeyLabel.F9, 0x65 },
    { KeyLabel.F11, 0x67 },
    { KeyLabel.F13, 0x69 },
    { KeyLabel.F16, 0x6A },
    { KeyLabel.F14, 0x6B },
    { KeyLabel.F10, 0x6D },
    { KeyLabel.F12, 0x6F },
    { KeyLabel.F15, 0x71 },
    { KeyLabel.Insert, 0x72 },
    { KeyLabel.Home, 0x73 },
    { KeyLabel.PageUp, 0x74 },
    { KeyLabel.Delete, 0x75 },
    { KeyLabel.End, 0x77 },
    { KeyLabel.F2, 0x78 },
    { KeyLabel.PageDown, 0x79 },
    { KeyLabel.F1, 0x7A },
    { KeyLabel.ArrowLeft, 0x7B },
    { KeyLabel.ArrowRight, 0x7C },
    { KeyLabel.ArrowDown, 0x7D },
    { KeyLabel.ArrowUp, 0x7E },
  };

  public static List<RecordedInput> NormalizeForPlayback(
    List<RecordedInput> inputs,
    ReplayMetadata meta,
    out int dropped
  )
  {
    dropped = 0;
    if (inputs == null)
      return new List<RecordedInput>();

    if (string.Equals(meta?.inputKeySpace, NativeKeySpace, StringComparison.OrdinalIgnoreCase))
    {
      return inputs;
    }

    List<RecordedInput> converted = new List<RecordedInput>(inputs.Count);
    foreach (RecordedInput input in inputs)
    {
      if (!TryConvertKeyLabel((KeyLabel)input.Key, out int nativeKeyCode))
      {
        dropped++;
        continue;
      }

      converted.Add(new RecordedInput(input.TimeUs, nativeKeyCode, input.Flags));
    }

    return converted;
  }

  public static bool TryConvertKeyLabel(KeyLabel label, out int nativeKeyCode)
  {
    nativeKeyCode = 0;
    if (label == KeyLabel.Unknown)
      return false;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      if (MacVirtualKeyCodes.TryGetValue(label, out ushort macKeyCode))
      {
        nativeKeyCode = macKeyCode;
        return true;
      }

      try
      {
        ushort fallbackKeyCode = SkyHookKeyMapper.KeyLabelToNativeKeyCode(label);
        if (fallbackKeyCode == 0)
          return false;

        nativeKeyCode = fallbackKeyCode;
        return true;
      }
      catch
      {
        return false;
      }
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      try
      {
        ushort virtualKey = SkyHookKeyMapper.KeyLabelToNativeKeyCode(label);
        if (virtualKey == 0)
          return false;

        nativeKeyCode = virtualKey;
        return true;
      }
      catch
      {
        return false;
      }
    }

    return false;
  }

  public static bool TryConvertSkyHookHidUsage(int hidUsage, out int nativeKeyCode)
  {
    nativeKeyCode = 0;
    return HidUsageLabels.TryGetValue(hidUsage, out KeyLabel label) && TryConvertKeyLabel(label, out nativeKeyCode);
  }

  private static Dictionary<int, KeyLabel> CreateHidUsageLabels()
  {
    Dictionary<int, KeyLabel> labels = new Dictionary<int, KeyLabel>();

    KeyLabel[] letters =
    {
      KeyLabel.A,
      KeyLabel.B,
      KeyLabel.C,
      KeyLabel.D,
      KeyLabel.E,
      KeyLabel.F,
      KeyLabel.G,
      KeyLabel.H,
      KeyLabel.I,
      KeyLabel.J,
      KeyLabel.K,
      KeyLabel.L,
      KeyLabel.M,
      KeyLabel.N,
      KeyLabel.O,
      KeyLabel.P,
      KeyLabel.Q,
      KeyLabel.R,
      KeyLabel.S,
      KeyLabel.T,
      KeyLabel.U,
      KeyLabel.V,
      KeyLabel.W,
      KeyLabel.X,
      KeyLabel.Y,
      KeyLabel.Z,
    };
    for (int i = 0; i < letters.Length; i++)
      labels[4 + i] = letters[i];

    KeyLabel[] digits =
    {
      KeyLabel.Alpha1,
      KeyLabel.Alpha2,
      KeyLabel.Alpha3,
      KeyLabel.Alpha4,
      KeyLabel.Alpha5,
      KeyLabel.Alpha6,
      KeyLabel.Alpha7,
      KeyLabel.Alpha8,
      KeyLabel.Alpha9,
      KeyLabel.Alpha0,
    };
    for (int i = 0; i < digits.Length; i++)
      labels[30 + i] = digits[i];

    labels[40] = KeyLabel.Enter;
    labels[41] = KeyLabel.Escape;
    labels[42] = KeyLabel.Backspace;
    labels[43] = KeyLabel.Tab;
    labels[44] = KeyLabel.Space;
    labels[45] = KeyLabel.Minus;
    labels[46] = KeyLabel.Equal;
    labels[47] = KeyLabel.LeftBrace;
    labels[48] = KeyLabel.RightBrace;
    labels[49] = KeyLabel.BackSlash;
    labels[50] = KeyLabel.BackSlash;
    labels[51] = KeyLabel.Semicolon;
    labels[52] = KeyLabel.Apostrophe;
    labels[53] = KeyLabel.Grave;
    labels[54] = KeyLabel.Comma;
    labels[55] = KeyLabel.Dot;
    labels[56] = KeyLabel.Slash;
    labels[57] = KeyLabel.CapsLock;

    KeyLabel[] functionKeys =
    {
      KeyLabel.F1,
      KeyLabel.F2,
      KeyLabel.F3,
      KeyLabel.F4,
      KeyLabel.F5,
      KeyLabel.F6,
      KeyLabel.F7,
      KeyLabel.F8,
      KeyLabel.F9,
      KeyLabel.F10,
      KeyLabel.F11,
      KeyLabel.F12,
    };
    for (int i = 0; i < functionKeys.Length; i++)
      labels[58 + i] = functionKeys[i];

    labels[70] = KeyLabel.PrintScreen;
    labels[71] = KeyLabel.ScrollLock;
    labels[72] = KeyLabel.PauseBreak;
    labels[73] = KeyLabel.Insert;
    labels[74] = KeyLabel.Home;
    labels[75] = KeyLabel.PageUp;
    labels[76] = KeyLabel.Delete;
    labels[77] = KeyLabel.End;
    labels[78] = KeyLabel.PageDown;
    labels[79] = KeyLabel.ArrowRight;
    labels[80] = KeyLabel.ArrowLeft;
    labels[81] = KeyLabel.ArrowDown;
    labels[82] = KeyLabel.ArrowUp;
    labels[83] = KeyLabel.NumLock;
    labels[84] = KeyLabel.KeypadSlash;
    labels[85] = KeyLabel.KeypadAsterisk;
    labels[86] = KeyLabel.KeypadMinus;
    labels[87] = KeyLabel.KeypadPlus;
    labels[88] = KeyLabel.KeypadEnter;

    KeyLabel[] keypadDigits =
    {
      KeyLabel.Keypad1,
      KeyLabel.Keypad2,
      KeyLabel.Keypad3,
      KeyLabel.Keypad4,
      KeyLabel.Keypad5,
      KeyLabel.Keypad6,
      KeyLabel.Keypad7,
      KeyLabel.Keypad8,
      KeyLabel.Keypad9,
      KeyLabel.Keypad0,
    };
    for (int i = 0; i < keypadDigits.Length; i++)
      labels[89 + i] = keypadDigits[i];
    labels[99] = KeyLabel.KeypadDot;
    labels[100] = KeyLabel.BackSlash;

    KeyLabel[] extendedFunctionKeys =
    {
      KeyLabel.F13,
      KeyLabel.F14,
      KeyLabel.F15,
      KeyLabel.F16,
      KeyLabel.F17,
      KeyLabel.F18,
      KeyLabel.F19,
      KeyLabel.F20,
      KeyLabel.F21,
      KeyLabel.F22,
      KeyLabel.F23,
      KeyLabel.F24,
    };
    for (int i = 0; i < extendedFunctionKeys.Length; i++)
      labels[104 + i] = extendedFunctionKeys[i];

    labels[224] = KeyLabel.LControl;
    labels[225] = KeyLabel.LShift;
    labels[226] = KeyLabel.LAlt;
    labels[227] = KeyLabel.Super;
    labels[228] = KeyLabel.RControl;
    labels[229] = KeyLabel.RShift;
    labels[230] = KeyLabel.RAlt;
    labels[231] = KeyLabel.Super;
    return labels;
  }
}
