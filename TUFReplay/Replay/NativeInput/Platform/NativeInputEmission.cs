namespace TUFReplay.Replay.NativeInput;

public readonly struct NativeInputEmission
{
  public readonly int Key;
  public readonly bool Down;
  public readonly bool ExtendedKey;
  public readonly int NativeCode;
  public readonly ulong NativeFlags;

  public NativeInputEmission(
    int key,
    bool down,
    bool extendedKey = false,
    int nativeCode = -1,
    ulong nativeFlags = 0
  )
  {
    Key = key;
    Down = down;
    ExtendedKey = extendedKey;
    NativeCode = nativeCode;
    NativeFlags = nativeFlags;
  }
}
