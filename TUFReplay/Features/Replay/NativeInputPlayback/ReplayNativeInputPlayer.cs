using System.Collections.Generic;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.NativeInput;

namespace TUFReplay.Features.Replay;

public sealed class ReplayNativeInputPlayer
{
  private readonly ReplayInputScheduler _scheduler;
  private readonly INativeInputEmitter _emitter;
  private readonly INativeInputFocusGuard _focusGuard;
  private readonly HashSet<int> _heldKeys = new HashSet<int>();

  internal ReplayNativeInputPlayer(
    ReplayInputScheduler scheduler,
    INativeInputEmitter emitter,
    INativeInputFocusGuard focusGuard
  )
  {
    _scheduler = scheduler ?? throw new System.ArgumentNullException(nameof(scheduler));
    _emitter = emitter ?? throw new System.ArgumentNullException(nameof(emitter));
    _focusGuard = focusGuard ?? throw new System.ArgumentNullException(nameof(focusGuard));
  }

  public void Reset()
  {
    ReleaseAll();
    _scheduler.Reset();
  }

  public int ResetTo(long nowUs)
  {
    ReleaseAll();
    List<int> heldKeys = _scheduler.SeekTo(nowUs);

    if (!_focusGuard.IsStable(out _))
      return 0;

    int restored = 0;

    foreach (int key in heldKeys)
    {
      if (!_emitter.Emit(key, true))
        continue;

      _heldKeys.Add(key);
      restored++;
    }

    return restored;
  }

  public int Tick(long nowUs)
  {
    if (!_focusGuard.IsStable(out _))
    {
      SkipTo(nowUs);
      return 0;
    }

    List<RecordedInput> due = _scheduler.PopDue(nowUs);
    int emitted = 0;

    foreach (RecordedInput input in due)
    {
      if (!input.Async)
        continue;
      if (!_emitter.Emit(input.Key, input.Down))
        continue;

      if (input.Down)
        _heldKeys.Add(input.Key);
      else
        _heldKeys.Remove(input.Key);

      emitted++;
    }

    return emitted;
  }

  public bool CanEmit(out string reason)
  {
    return _focusGuard.IsStable(out reason);
  }

  public int SkipTo(long nowUs)
  {
    ReleaseAll();
    return _scheduler.SkipDue(nowUs);
  }

  public string DescribeFocus()
  {
    return _focusGuard.Describe();
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
