using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkyHook;

namespace TUFReplay.Replay.NativeInput;

internal static class NativeInputKeyCodeMapper
{
  public const string NativeKeySpace = "os-native-key-code";

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
    { KeyLabel.ArrowUp, 0x7E }
  };

  public static List<ReplayInputEvent> NormalizeForPlayback(
    List<ReplayInputEvent> inputs,
    ReplayRecordMeta meta,
    out int dropped
  )
  {
    dropped = 0;
    if (inputs == null) return new List<ReplayInputEvent>();

    if (string.Equals(meta?.inputKeySpace, NativeKeySpace, StringComparison.OrdinalIgnoreCase))
    {
      return inputs;
    }

    List<ReplayInputEvent> converted = new List<ReplayInputEvent>(inputs.Count);
    foreach (ReplayInputEvent input in inputs)
    {
      if (!TryConvertLegacyKeyLabel(input.Key, out int nativeKeyCode))
      {
        dropped++;
        continue;
      }

      converted.Add(new ReplayInputEvent(input.TimeUs, nativeKeyCode, input.Flags));
    }

    return converted;
  }

  private static bool TryConvertLegacyKeyLabel(int key, out int nativeKeyCode)
  {
    nativeKeyCode = 0;
    KeyLabel label = (KeyLabel)key;
    if (label == KeyLabel.Unknown) return false;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      if (!MacVirtualKeyCodes.TryGetValue(label, out ushort macKeyCode)) return false;
      nativeKeyCode = macKeyCode;
      return true;
    }

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      try
      {
        ushort virtualKey = SkyHookKeyMapper.KeyLabelToNativeKeyCode(label);
        if (virtualKey == 0) return false;

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
}
