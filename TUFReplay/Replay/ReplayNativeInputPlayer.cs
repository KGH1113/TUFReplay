using System.Collections.Generic;

namespace TUFReplay.Replay;

public sealed class ReplayNativeInputPlayer
{
  private readonly ReplayInputScheduler _scheduler;
  private readonly NativeInput.INativeInputEmitter _emitter;
  private readonly HashSet<int> _heldKeys = new HashSet<int>();

  public ReplayNativeInputPlayer(ReplayInputScheduler scheduler, NativeInput.INativeInputEmitter emitter)
  {
    _scheduler = scheduler;
    _emitter = emitter;
  }

  public void Reset()
  {
    ReleaseAll();
    _scheduler.Reset();
  }

  public int ResetTo(long nowUs)
  {
    ReleaseAll();

    if (!UnityEngine.Application.isFocused)
    {
      _scheduler.Reset();
      return 0;
    }

    int restored = 0;
    List<int> heldKeys = _scheduler.SeekTo(nowUs);

    foreach (int key in heldKeys)
    {
      if (!_emitter.Emit(key, true)) continue;

      _heldKeys.Add(key);
      restored++;
    }

    return restored;
  }

  public int Tick(long nowUs)
  {
    if (!UnityEngine.Application.isFocused)
    {
      ReleaseAll();
      return 0;
    }

    List<ReplayInputEvent> due = _scheduler.PopDue(nowUs);
    int emitted = 0;

    foreach (ReplayInputEvent input in due)
    {
      if (!input.Async) continue;
      if (!_emitter.Emit(input.Key, input.Down)) continue;

      if (input.Down)
        _heldKeys.Add(input.Key);
      else
        _heldKeys.Remove(input.Key);

      emitted++;
    }

    return emitted;
  }

  public void ReleaseAll()
  {
    foreach (int key in _heldKeys)
    {
      _emitter.Emit(key, false);
    }

    _heldKeys.Clear();
  }
}
