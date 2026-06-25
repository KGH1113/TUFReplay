using TUFReplay.Recording;

namespace TUFReplay.Replay;

public readonly struct ReplayInputEvent
{
  public readonly long TimeUs;
  public readonly int Key;
  public readonly RecordInputFlags Flags;

  public bool Down => (Flags & RecordInputFlags.Down) != 0;
  public bool Async => (Flags & RecordInputFlags.Async) != 0;
  public bool PassedHook => (Flags & RecordInputFlags.PassedHook) != 0;
  public bool MainCandidate => (Flags & RecordInputFlags.MainCandidate) != 0;
  public bool GameplayCounted => (Flags & RecordInputFlags.GameplayCounted) != 0;

  public ReplayInputEvent(long timeUs, int key, RecordInputFlags flags)
  {
    TimeUs = timeUs;
    Key = key;
    Flags = flags;
  }
}
