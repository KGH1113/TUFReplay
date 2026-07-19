using System.Collections.Generic;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Features.Replay;

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
    lock (_gate)
    {
      _nextIndex = 0;

      List<int> heldKeys = new List<int>();
      HashSet<int> heldSet = new HashSet<int>();

      while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
      {
        RecordedInput input = _events[_nextIndex];

        if (input.Async)
        {
          if (input.Down)
          {
            if (heldSet.Add(input.Key))
              heldKeys.Add(input.Key);
          }
          else if (heldSet.Remove(input.Key))
          {
            heldKeys.Remove(input.Key);
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
