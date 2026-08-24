using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SkyHook;
using TUFReplay.Replay.Models;

namespace TUFReplay.Shared.NativeInput;

internal static class NativeInputKeyCodeMapper
{
  public const string NativeKeySpace = "os-native-key-code";
  public const string CorruptedNativeStateMigrationCapture = "skyhook-native-events";
  public const string LegacyWindowsThreadStateCapture = "skyhook-events-high-resolution";
  public const string PhysicalStateCapture = "skyhook-events-high-resolution-physical-state";

  private static readonly Dictionary<int, KeyLabel> HidUsageLabels = CreateHidUsageLabels();
  private static readonly object RepairMapLock = new object();
  private static Dictionary<int, int> _reversibleMigratedNativeCodes;
  private static HashSet<int> _ambiguousMigratedNativeCodes;

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
  private static readonly Dictionary<ushort, KeyLabel> MacVirtualKeyLabels = CreateReverseMap(MacVirtualKeyCodes);

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
      List<RecordedInput> normalized = inputs;
      if (
        meta.formatVersion == 3
        && string.Equals(meta.inputCapture, CorruptedNativeStateMigrationCapture, StringComparison.OrdinalIgnoreCase)
      )
        normalized = RepairCorruptedNativeStateMigration(inputs, out dropped);
      else if (ShouldRemoveLegacyWindowsInitialState(inputs, meta))
        normalized = RemoveFirstTimestampGroup(inputs, out dropped);

      string currentPlatform = CurrentPlatform();
      if (
        !string.IsNullOrWhiteSpace(meta.inputNativePlatform)
        && string.Equals(meta.inputFormat, RecordedRunPayload.NativeInputFormatV2, StringComparison.Ordinal)
        && currentPlatform != "unsupported"
        && !string.Equals(meta.inputNativePlatform, currentPlatform, StringComparison.OrdinalIgnoreCase)
      )
      {
        List<RecordedInput> crossPlatformInputs = ConvertNativePlatform(
          normalized,
          meta.inputNativePlatform,
          out int crossDropped
        );
        dropped += crossDropped;
        return crossPlatformInputs;
      }
      return normalized;
    }

    List<RecordedInput> converted = new List<RecordedInput>(inputs.Count);
    foreach (RecordedInput input in inputs)
    {
      KeyLabel label = (KeyLabel)input.Key;
      if (!TryConvertKeyLabel(label, out int nativeKeyCode))
      {
        dropped++;
        continue;
      }

      RecordInputFlags flags = input.Flags;
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsNativeInputKey.IsExtendedLabel(label))
        flags |= RecordInputFlags.ExtendedKey;
      converted.Add(new RecordedInput(input.TimeUs, nativeKeyCode, flags));
    }

    return converted;
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
        !TryGetSourceKeyLabel(sourcePlatform, input.Key, out KeyLabel label)
        || !TryConvertKeyLabel(label, out int currentKey)
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

  private static bool TryGetSourceKeyLabel(string sourcePlatform, int nativeKey, out KeyLabel label)
  {
    label = KeyLabel.Unknown;
    if (nativeKey < 0 || nativeKey > ushort.MaxValue)
      return false;
    if (string.Equals(sourcePlatform, "macos", StringComparison.OrdinalIgnoreCase))
      return MacVirtualKeyLabels.TryGetValue((ushort)nativeKey, out label);
    if (!string.Equals(sourcePlatform, "windows", StringComparison.OrdinalIgnoreCase))
      return false;
    return TryGetWindowsKeyLabel(nativeKey, out label);
  }

  private static bool TryGetWindowsKeyLabel(int key, out KeyLabel label)
  {
    if (key >= 0x41 && key <= 0x5A)
    {
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
      label = letters[key - 0x41];
      return true;
    }
    if (key >= 0x30 && key <= 0x39)
    {
      KeyLabel[] digits =
      {
        KeyLabel.Alpha0,
        KeyLabel.Alpha1,
        KeyLabel.Alpha2,
        KeyLabel.Alpha3,
        KeyLabel.Alpha4,
        KeyLabel.Alpha5,
        KeyLabel.Alpha6,
        KeyLabel.Alpha7,
        KeyLabel.Alpha8,
        KeyLabel.Alpha9,
      };
      label = digits[key - 0x30];
      return true;
    }
    if (key >= 0x70 && key <= 0x87)
    {
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
      label = functionKeys[key - 0x70];
      return true;
    }
    if (key >= 0x60 && key <= 0x69)
    {
      KeyLabel[] keypadDigits =
      {
        KeyLabel.Keypad0,
        KeyLabel.Keypad1,
        KeyLabel.Keypad2,
        KeyLabel.Keypad3,
        KeyLabel.Keypad4,
        KeyLabel.Keypad5,
        KeyLabel.Keypad6,
        KeyLabel.Keypad7,
        KeyLabel.Keypad8,
        KeyLabel.Keypad9,
      };
      label = keypadDigits[key - 0x60];
      return true;
    }

    switch (key)
    {
      case 0x08: label = KeyLabel.Backspace; return true;
      case 0x09: label = KeyLabel.Tab; return true;
      case 0x0D: label = KeyLabel.Enter; return true;
      case 0x13: label = KeyLabel.PauseBreak; return true;
      case 0x14: label = KeyLabel.CapsLock; return true;
      case 0x1B: label = KeyLabel.Escape; return true;
      case 0x20: label = KeyLabel.Space; return true;
      case 0x21: label = KeyLabel.PageUp; return true;
      case 0x22: label = KeyLabel.PageDown; return true;
      case 0x23: label = KeyLabel.End; return true;
      case 0x24: label = KeyLabel.Home; return true;
      case 0x25: label = KeyLabel.ArrowLeft; return true;
      case 0x26: label = KeyLabel.ArrowUp; return true;
      case 0x27: label = KeyLabel.ArrowRight; return true;
      case 0x28: label = KeyLabel.ArrowDown; return true;
      case 0x2C: label = KeyLabel.PrintScreen; return true;
      case 0x2D: label = KeyLabel.Insert; return true;
      case 0x2E: label = KeyLabel.Delete; return true;
      case 0x5B:
      case 0x5C: label = KeyLabel.Super; return true;
      case 0x6A: label = KeyLabel.KeypadAsterisk; return true;
      case 0x6B: label = KeyLabel.KeypadPlus; return true;
      case 0x6D: label = KeyLabel.KeypadMinus; return true;
      case 0x6E: label = KeyLabel.KeypadDot; return true;
      case 0x6F: label = KeyLabel.KeypadSlash; return true;
      case 0x90: label = KeyLabel.NumLock; return true;
      case 0x91: label = KeyLabel.ScrollLock; return true;
      case 0xA0: label = KeyLabel.LShift; return true;
      case 0xA1: label = KeyLabel.RShift; return true;
      case 0xA2: label = KeyLabel.LControl; return true;
      case 0xA3: label = KeyLabel.RControl; return true;
      case 0xA4: label = KeyLabel.LAlt; return true;
      case 0xA5: label = KeyLabel.RAlt; return true;
      case 0xBA: label = KeyLabel.Semicolon; return true;
      case 0xBB: label = KeyLabel.Equal; return true;
      case 0xBC: label = KeyLabel.Comma; return true;
      case 0xBD: label = KeyLabel.Minus; return true;
      case 0xBE: label = KeyLabel.Dot; return true;
      case 0xBF: label = KeyLabel.Slash; return true;
      case 0xC0: label = KeyLabel.Grave; return true;
      case 0xDB: label = KeyLabel.LeftBrace; return true;
      case 0xDC: label = KeyLabel.BackSlash; return true;
      case 0xDD: label = KeyLabel.RightBrace; return true;
      case 0xDE: label = KeyLabel.Apostrophe; return true;
      default: label = KeyLabel.Unknown; return false;
    }
  }

  private static Dictionary<ushort, KeyLabel> CreateReverseMap(Dictionary<KeyLabel, ushort> source)
  {
    Dictionary<ushort, KeyLabel> result = new Dictionary<ushort, KeyLabel>();
    foreach (KeyValuePair<KeyLabel, ushort> pair in source)
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

  private static bool ShouldRemoveLegacyWindowsInitialState(List<RecordedInput> inputs, ReplayMetadata meta)
  {
    if (
      inputs.Count < 2
      || inputs[0].TimeUs >= 0
      || !string.Equals(meta?.inputNativePlatform, "windows", StringComparison.OrdinalIgnoreCase)
      || !string.Equals(meta.inputCapture, LegacyWindowsThreadStateCapture, StringComparison.OrdinalIgnoreCase)
    )
      return false;

    long firstTimeUs = inputs[0].TimeUs;
    int count = 0;
    while (count < inputs.Count && inputs[count].TimeUs == firstTimeUs)
    {
      if ((inputs[count].Flags & RecordInputFlags.Down) == 0)
        return false;
      count++;
    }

    return count >= 2;
  }

  private static List<RecordedInput> RemoveFirstTimestampGroup(List<RecordedInput> inputs, out int dropped)
  {
    long firstTimeUs = inputs[0].TimeUs;
    dropped = 0;
    while (dropped < inputs.Count && inputs[dropped].TimeUs == firstTimeUs)
      dropped++;

    return inputs.GetRange(dropped, inputs.Count - dropped);
  }

  private static List<RecordedInput> RepairCorruptedNativeStateMigration(List<RecordedInput> inputs, out int dropped)
  {
    dropped = 0;
    EnsureRepairMap();

    List<RecordedInput> repaired = new List<RecordedInput>(inputs.Count);
    foreach (RecordedInput input in inputs)
    {
      if (
        _ambiguousMigratedNativeCodes.Contains(input.Key)
        || !_reversibleMigratedNativeCodes.TryGetValue(input.Key, out int originalNativeKeyCode)
      )
      {
        dropped++;
        continue;
      }

      RecordInputFlags flags = input.Flags & ~RecordInputFlags.ExtendedKey;
      if (
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsNativeInputKey.IsExtended(originalNativeKeyCode)
      )
        flags |= RecordInputFlags.ExtendedKey;
      repaired.Add(new RecordedInput(input.TimeUs, originalNativeKeyCode, flags));
    }

    return repaired;
  }

  private static void EnsureRepairMap()
  {
    lock (RepairMapLock)
    {
      if (_reversibleMigratedNativeCodes != null)
        return;

      Dictionary<int, int> reversible = new Dictionary<int, int>();
      HashSet<int> ambiguous = new HashSet<int>();
      foreach (KeyValuePair<int, KeyLabel> pair in HidUsageLabels)
      {
        if (!TryConvertKeyLabel(pair.Value, out int migratedNativeKeyCode))
          continue;

        if (reversible.TryGetValue(migratedNativeKeyCode, out int existing) && existing != pair.Key)
        {
          reversible.Remove(migratedNativeKeyCode);
          ambiguous.Add(migratedNativeKeyCode);
          continue;
        }

        if (!ambiguous.Contains(migratedNativeKeyCode))
          reversible[migratedNativeKeyCode] = pair.Key;
      }

      _ambiguousMigratedNativeCodes = ambiguous;
      _reversibleMigratedNativeCodes = reversible;
    }
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
    return TryConvertSkyHookHidUsage(hidUsage, out nativeKeyCode, out _);
  }

  public static bool TryConvertSkyHookHidUsage(int hidUsage, out int nativeKeyCode, out bool extendedKey)
  {
    nativeKeyCode = 0;
    extendedKey = false;
    if (!HidUsageLabels.TryGetValue(hidUsage, out KeyLabel label) || !TryConvertKeyLabel(label, out nativeKeyCode))
      return false;

    extendedKey = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsNativeInputKey.IsExtendedLabel(label);
    return true;
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
