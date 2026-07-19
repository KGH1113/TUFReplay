using System;
using System.Runtime.InteropServices;

namespace TUFReplay.Infrastructure.NativeInput;

public sealed class WindowsNativeInputEmitter : INativeInputEmitter
{
  private const uint InputKeyboard = 1;
  private const uint KeyEventExtendedKey = 0x0001;
  private const uint KeyEventKeyUp = 0x0002;

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

      uint flags = emission.Down ? 0u : KeyEventKeyUp;
      if (IsExtendedKey((ushort)emission.Key))
        flags |= KeyEventExtendedKey;

      _inputBuffer[i] = new Input
      {
        Type = InputKeyboard,
        Union = new InputUnion
        {
          Keyboard = new KeyboardInput { VirtualKey = (ushort)emission.Key, Flags = flags },
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
