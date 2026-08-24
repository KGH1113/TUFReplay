namespace TUFReplay.Replay.Models;

public readonly struct RecordedInput
{
  public readonly long TimeUs;
  public readonly int Key;
  public readonly RecordInputFlags Flags;
  public readonly int NativeCode;
  public readonly ulong NativeFlags;

  public bool Down => (Flags & RecordInputFlags.Down) != 0;
  public bool Async => (Flags & RecordInputFlags.Async) != 0;
  public bool PassedHook => (Flags & RecordInputFlags.PassedHook) != 0;
  public bool MainCandidate => (Flags & RecordInputFlags.MainCandidate) != 0;
  public bool GameplayCounted => (Flags & RecordInputFlags.GameplayCounted) != 0;
  public bool ExtendedKey => (Flags & RecordInputFlags.ExtendedKey) != 0;

  public bool HasNativeMetadata => NativeCode >= 0;

  public RecordedInput(long timeUs, int key, RecordInputFlags flags, int nativeCode = -1, ulong nativeFlags = 0)
  {
    TimeUs = timeUs;
    Key = key;
    Flags = flags;
    NativeCode = nativeCode;
    NativeFlags = nativeFlags;
  }
}

[System.Flags]
public enum RecordInputFlags : ushort
{
  Down = 1 << 0,
  Async = 1 << 1,
  PassedHook = 1 << 2,
  MainCandidate = 1 << 3,
  GameplayCounted = 1 << 4,
  ExtendedKey = 1 << 5,
}
