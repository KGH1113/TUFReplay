using System.Collections.Generic;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Replay.NativeInput;

public class ReplayInputScheduler
{
  private readonly object _gate = new object();
  private readonly List<RecordedInput> _events;
  private int _nextIndex;

  public ReplayInputScheduler(List<RecordedInput> events)
  {
    _events = events ?? new List<RecordedInput>();
    _nextIndex = 0;
  }

  public int Count => _events.Count;
  public int NextIndex
  {
    get
    {
      lock (_gate)
        return _nextIndex;
    }
  }
  public bool Finished
  {
    get
    {
      lock (_gate)
        return _nextIndex >= _events.Count;
    }
  }

  public void Reset()
  {
    lock (_gate)
      _nextIndex = 0;
  }

  public List<int> SeekToState(long nowUs)
  {
    List<NativeInputKey> nativeState = SeekToNativeState(nowUs);
    List<int> heldKeys = new List<int>(nativeState.Count);
    for (int i = 0; i < nativeState.Count; i++)
      heldKeys.Add(nativeState[i].Key);
    return heldKeys;
  }

  internal List<NativeInputKey> SeekToNativeState(long nowUs)
  {
    lock (_gate)
    {
      _nextIndex = 0;

      List<NativeInputKey> heldKeys = new List<NativeInputKey>();
      HashSet<NativeInputKey> heldSet = new HashSet<NativeInputKey>();

      while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
      {
        RecordedInput input = _events[_nextIndex];

        if (input.Async)
        {
          NativeInputKey key = new NativeInputKey(
            input.Key,
            input.ExtendedKey,
            input.NativeCode,
            input.NativeFlags
          );
          if (input.Down)
          {
            if (heldSet.Add(key))
              heldKeys.Add(key);
          }
          else if (heldSet.Remove(key))
          {
            heldKeys.Remove(key);
          }
        }

        _nextIndex++;
      }

      return heldKeys;
    }
  }

  public int CopyNextTimestampGroup(List<RecordedInput> destination)
  {
    if (destination == null)
      throw new System.ArgumentNullException(nameof(destination));

    lock (_gate)
    {
      destination.Clear();
      if (_nextIndex >= _events.Count)
        return 0;

      long timestamp = _events[_nextIndex].TimeUs;
      while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs == timestamp)
      {
        destination.Add(_events[_nextIndex]);
        _nextIndex++;
      }

      return destination.Count;
    }
  }

  public RecordedInput? PeekNext()
  {
    lock (_gate)
    {
      if (_nextIndex >= _events.Count)
        return null;
      return _events[_nextIndex];
    }
  }
}
