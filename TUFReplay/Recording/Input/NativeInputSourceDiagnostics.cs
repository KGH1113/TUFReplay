namespace TUFReplay.Recording.Input;

internal readonly struct NativeInputSourceDiagnostics
{
  public readonly long Callbacks;
  public readonly long Repeats;
  public readonly long Unmapped;
  public readonly int Devices;
  public readonly int QueueDepth;

  public NativeInputSourceDiagnostics(long callbacks, long repeats, long unmapped, int devices, int queueDepth)
  {
    Callbacks = callbacks;
    Repeats = repeats;
    Unmapped = unmapped;
    Devices = devices;
    QueueDepth = queueDepth;
  }
}
