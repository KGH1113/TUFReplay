namespace TUFReplay.Recording.Input;

internal readonly struct NativeInputTransition
{
  public readonly long CaptureTimestampTicks;
  public readonly long TimestampNs;
  public readonly int Key;
  public readonly int NativeCode;
  public readonly ulong NativeFlags;
  public readonly bool Down;
  public readonly bool ExtendedKey;

  public NativeInputTransition(
    long captureTimestampTicks,
    long timestampNs,
    int key,
    bool down,
    bool extendedKey = false,
    int nativeCode = -1,
    ulong nativeFlags = 0
  )
  {
    CaptureTimestampTicks = captureTimestampTicks;
    TimestampNs = timestampNs;
    Key = key;
    NativeCode = nativeCode;
    NativeFlags = nativeFlags;
    Down = down;
    ExtendedKey = extendedKey;
  }
}
