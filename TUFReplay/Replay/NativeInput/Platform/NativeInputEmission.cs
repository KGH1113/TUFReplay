namespace TUFReplay.Replay.NativeInput;

public readonly struct NativeInputEmission
{
  public readonly int Key;
  public readonly bool Down;
  public readonly bool ExtendedKey;

  public NativeInputEmission(int key, bool down, bool extendedKey = false)
  {
    Key = key;
    Down = down;
    ExtendedKey = extendedKey;
  }
}
