using System.Collections.Generic;

namespace TUFReplay.Replay;

public class ReplayInputScheduler
{
  private readonly List<ReplayInputEvent> _events;
  private int _nextIndex;

  public ReplayInputScheduler(List<ReplayInputEvent> events)
  {
    _events = events ?? new List<ReplayInputEvent>();
    _nextIndex = 0;
  }

  public int Count => _events.Count;
  public int NextIndex => _nextIndex;
  public bool Finished => _nextIndex >= _events.Count;

  public void Reset()
  {
    _nextIndex = 0;
  }

  public List<int> SeekTo(long nowUs)
  {
    _nextIndex = 0;

    List<int> heldKeys = new List<int>();
    HashSet<int> heldSet = new HashSet<int>();

    while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
    {
      ReplayInputEvent input = _events[_nextIndex];

      if (input.Async)
      {
        if (input.Down)
        {
          if (heldSet.Add(input.Key))
          {
            heldKeys.Add(input.Key);
          }
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

  public List<ReplayInputEvent> PopDue(long nowUs)
  {
    List<ReplayInputEvent> due = new List<ReplayInputEvent>();

    while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
    {
      due.Add(_events[_nextIndex]);
      _nextIndex++;
    }

    return due;
  }

  public ReplayInputEvent? PeekNext()
  {
    if (_nextIndex >= _events.Count) return null;
    return _events[_nextIndex];
  }
}
