namespace TUFReplay.Recording.Input;

internal readonly struct NativeInputTransition
{
  public readonly long TimestampNs;
  public readonly int Key;
  public readonly bool Down;
  public readonly bool ExtendedKey;

  public NativeInputTransition(long timestampNs, int key, bool down, bool extendedKey = false)
  {
    TimestampNs = timestampNs;
    Key = key;
    Down = down;
    ExtendedKey = extendedKey;
  }
}
