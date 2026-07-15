using System.Collections.Generic;
using TUFReplay.Domain.ReplayData;

namespace TUFReplay.Features.Replay;

public class ReplayInputScheduler
{
  private readonly List<RecordedInput> _events;
  private int _nextIndex;

  public ReplayInputScheduler(List<RecordedInput> events)
  {
    _events = events ?? new List<RecordedInput>();
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
      RecordedInput input = _events[_nextIndex];

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

  public List<RecordedInput> PopDue(long nowUs)
  {
    List<RecordedInput> due = new List<RecordedInput>();

    while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
    {
      due.Add(_events[_nextIndex]);
      _nextIndex++;
    }

    return due;
  }

  public int SkipDue(long nowUs)
  {
    int startIndex = _nextIndex;

    while (_nextIndex < _events.Count && _events[_nextIndex].TimeUs <= nowUs)
      _nextIndex++;

    return _nextIndex - startIndex;
  }

  public RecordedInput? PeekNext()
  {
    if (_nextIndex >= _events.Count)
      return null;
    return _events[_nextIndex];
  }
}
