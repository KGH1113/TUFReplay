namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal readonly struct NativeInputTransition
{
  public readonly long TimestampNs;
  public readonly int Key;
  public readonly bool Down;

  public NativeInputTransition(long timestampNs, int key, bool down)
  {
    TimestampNs = timestampNs;
    Key = key;
    Down = down;
  }
}
