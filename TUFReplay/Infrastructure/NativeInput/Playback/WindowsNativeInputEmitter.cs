using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput;

public sealed class WindowsNativeInputEmitter : INativeInputEmitter
{
  private const uint InputKeyboard = 1;
  private const uint KeyEventExtendedKey = 0x0001;
  private const uint KeyEventKeyUp = 0x0002;
  private const uint KeyEventScanCode = 0x0008;
  private const uint MapVirtualKeyToScanCodeEx = 4;
  private const ushort PauseVirtualKey = 0x13;

  [StructLayout(LayoutKind.Sequential)]
  private struct Input
  {
    public uint Type;
    public InputUnion Union;
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct InputUnion
  {
    [FieldOffset(0)]
    public KeyboardInput Keyboard;

    // INPUT's native union is sized by MOUSEINPUT, which is larger than
    // KEYBDINPUT on both 32-bit and 64-bit Windows. Keeping this field in the
    // managed union makes Marshal.SizeOf<Input>() match sizeof(INPUT).
    [FieldOffset(0)]
    public MouseInput Mouse;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct KeyboardInput
  {
    public ushort VirtualKey;
    public ushort ScanCode;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MouseInput
  {
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public UIntPtr ExtraInfo;
  }

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

  [DllImport("user32.dll")]
  private static extern uint MapVirtualKeyW(uint code, uint mapType);

  private static readonly int InputSize = Marshal.SizeOf(typeof(Input));
  private Input[] _inputBuffer = new Input[32];

  public bool IsSupported(int key)
  {
    return key > 0 && key <= 255 && !IsBlockedKey((ushort)key);
  }

  public bool EmitBatch(NativeInputEmission[] emissions, int count)
  {
    if (emissions == null || count < 0 || count > emissions.Length)
      return false;
    if (count == 0)
      return true;

    EnsureCapacity(count);
    for (int i = 0; i < count; i++)
    {
      NativeInputEmission emission = emissions[i];
      if (!IsSupported(emission.Key))
        return false;

      _inputBuffer[i] = new Input
      {
        Type = InputKeyboard,
        Union = new InputUnion
        {
          Keyboard = CreateKeyboardInput(emission),
        },
      };
    }

    return SendInput((uint)count, _inputBuffer, InputSize) == (uint)count;
  }

  private void EnsureCapacity(int count)
  {
    if (_inputBuffer.Length >= count)
      return;

    int capacity = _inputBuffer.Length;
    while (capacity < count)
      capacity *= 2;
    _inputBuffer = new Input[capacity];
  }

  private static bool IsBlockedKey(ushort virtualKey)
  {
    switch (virtualKey)
    {
      case 16: // Generic Shift
      case 17: // Generic Ctrl
      case 18: // Generic Alt
        return true;
      default:
        return false;
    }
  }

  private static KeyboardInput CreateKeyboardInput(NativeInputEmission emission)
  {
    ushort virtualKey = (ushort)emission.Key;
    uint flags = emission.Down ? 0u : KeyEventKeyUp;

    // Pause uses the E1-prefixed sequence, which KEYEVENTF_EXTENDEDKEY cannot
    // represent. Let Windows synthesize it from VK_PAUSE instead of emitting a
    // truncated scan-code sequence.
    if (virtualKey == PauseVirtualKey)
    {
      return new KeyboardInput { VirtualKey = virtualKey, Flags = flags };
    }

    uint mappedScanCode = MapVirtualKeyW(virtualKey, MapVirtualKeyToScanCodeEx);
    if (mappedScanCode == 0)
    {
      if (emission.ExtendedKey || WindowsNativeInputKey.IsExtended(virtualKey))
        flags |= KeyEventExtendedKey;
      return new KeyboardInput { VirtualKey = virtualKey, Flags = flags };
    }

    bool mappedExtended = (mappedScanCode & 0xFF00u) == 0xE000u;
    flags |= KeyEventScanCode;
    if (emission.ExtendedKey || mappedExtended || WindowsNativeInputKey.IsExtended(virtualKey))
      flags |= KeyEventExtendedKey;

    return new KeyboardInput { ScanCode = (ushort)(mappedScanCode & 0xFFu), Flags = flags };
  }
}
