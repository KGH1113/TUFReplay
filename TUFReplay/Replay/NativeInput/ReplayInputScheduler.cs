using System.Collections.Generic;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Replay.NativeInput;

public class ReplayInputScheduler
{
  internal const int SeekCheckpointInterval = 2048;
  private readonly object _gate = new object();
  private readonly List<RecordedInput> _events;
  private readonly List<NativeInputKey> _initialHeldKeys;
  private readonly List<NativeInputKey[]> _checkpoints = new List<NativeInputKey[]>();
  private readonly bool _timestampsOrdered;
  private readonly int _indexedCount;
  private int _nextIndex;
  internal int LastSeekScannedEvents { get; private set; }

  public ReplayInputScheduler(List<RecordedInput> events)
  {
    _events = events ?? new List<RecordedInput>();
    _initialHeldKeys = InferInitialHeldKeys(_events);
    _indexedCount = _events.Count;
    _timestampsOrdered = BuildCheckpoints();
    _nextIndex = 0;
  }

  public int Count => _events.Count;
  public int InitialHeldCount => _initialHeldKeys.Count;
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
      int endIndex = 0;
      if (_timestampsOrdered && _indexedCount == _events.Count)
      {
        int low = 0;
        int high = _events.Count;
        while (low < high)
        {
          int middle = low + (high - low) / 2;
          if (_events[middle].TimeUs <= nowUs)
            low = middle + 1;
          else
            high = middle;
        }
        endIndex = low;
      }
      else
        while (endIndex < _events.Count && _events[endIndex].TimeUs <= nowUs)
          endIndex++;

      int checkpoint = _timestampsOrdered && _indexedCount == _events.Count ? endIndex / SeekCheckpointInterval : 0;
      var heldKeys = new List<NativeInputKey>(_checkpoints[checkpoint]);
      var heldSet = new HashSet<NativeInputKey>(heldKeys);
      int startIndex = checkpoint * SeekCheckpointInterval;
      LastSeekScannedEvents = endIndex - startIndex;
      for (int i = startIndex; i < endIndex; i++)
        ApplyTransition(_events[i], heldKeys, heldSet);
      _nextIndex = endIndex;

      return heldKeys;
    }
  }

  private bool BuildCheckpoints()
  {
    var heldKeys = new List<NativeInputKey>(_initialHeldKeys);
    var heldSet = new HashSet<NativeInputKey>(_initialHeldKeys);
    _checkpoints.Add(heldKeys.ToArray());
    for (int i = 0; i < _events.Count; i++)
    {
      if (i > 0 && _events[i].TimeUs < _events[i - 1].TimeUs)
        return false;
      ApplyTransition(_events[i], heldKeys, heldSet);
      if ((i + 1) % SeekCheckpointInterval == 0)
        _checkpoints.Add(heldKeys.ToArray());
    }
    return true;
  }

  private static void ApplyTransition(
    RecordedInput input,
    List<NativeInputKey> heldKeys,
    HashSet<NativeInputKey> heldSet
  )
  {
    if (!input.Async)
      return;
    var key = new NativeInputKey(input.Key, input.ExtendedKey, input.NativeCode, input.NativeFlags);
    if (input.Down)
    {
      if (heldSet.Add(key))
        heldKeys.Add(key);
    }
    else if (heldSet.Remove(key))
      heldKeys.Remove(key);
  }

  private static List<NativeInputKey> InferInitialHeldKeys(List<RecordedInput> events)
  {
    var initialHeldKeys = new List<NativeInputKey>();
    var seenKeys = new HashSet<NativeInputKey>();

    for (int i = 0; i < events.Count; i++)
    {
      RecordedInput input = events[i];
      if (!input.Async)
        continue;

      var key = new NativeInputKey(input.Key, input.ExtendedKey, input.NativeCode, input.NativeFlags);
      if (!seenKeys.Add(key))
        continue;

      // A key whose first recorded transition is up was already held when
      // the recording window opened. Preserve that implicit initial state
      // instead of normalizing every known key through synthetic up/down events.
      if (!input.Down)
        initialHeldKeys.Add(key);
    }

    return initialHeldKeys;
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
