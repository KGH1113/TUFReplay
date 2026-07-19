using System;
using TUFReplay.Infrastructure.NativeInput;

namespace TUFReplay.Features.Replay;

public sealed class ReplayNativeInputPlayer : IDisposable
{
  private readonly ReplayInputScheduler _scheduler;
  private readonly INativeInputFocusGuard _focusGuard;
  private readonly ReplayNativeInputPump _pump;
  private long _lastReportedEmitted;

  internal ReplayNativeInputPlayer(
    ReplayInputScheduler scheduler,
    INativeInputEmitter emitter,
    INativeInputFocusGuard focusGuard
  )
  {
    _scheduler = scheduler ?? throw new System.ArgumentNullException(nameof(scheduler));
    _focusGuard = focusGuard ?? throw new System.ArgumentNullException(nameof(focusGuard));
    _pump = new ReplayNativeInputPump(scheduler, emitter ?? throw new ArgumentNullException(nameof(emitter)));
  }

  public bool Finished => _scheduler.Finished;
  public ReplayNativeInputStats Stats => _pump.Snapshot;

  public void Reset()
  {
    _pump.Reset();
    _lastReportedEmitted = _pump.Snapshot.Emitted;
  }

  public int ResetTo(long nowUs, double timelineRate)
  {
    bool focusReady = _focusGuard.IsStable(out _);
    int restored = _pump.ResetTo(nowUs, timelineRate, focusReady);
    _lastReportedEmitted = _pump.Snapshot.Emitted;
    return restored;
  }

  public int Tick(long nowUs, double timelineRate)
  {
    bool focusReady = _focusGuard.IsStable(out _);
    _pump.Synchronize(nowUs, timelineRate, focusReady);
    ReplayNativeInputStats stats = _pump.Snapshot;
    long emitted = Math.Max(0L, stats.Emitted - _lastReportedEmitted);
    _lastReportedEmitted = stats.Emitted;
    return emitted > int.MaxValue ? int.MaxValue : (int)emitted;
  }

  public bool CanEmit(out string reason)
  {
    return _focusGuard.IsStable(out reason);
  }

  public int SkipTo(long nowUs)
  {
    return _pump.SuspendAt(nowUs);
  }

  public string DescribeFocus()
  {
    return _focusGuard.Describe();
  }

  public void ReleaseAll()
  {
    _pump.ReleaseAll();
  }

  public void Dispose()
  {
    _pump.Dispose();
  }
}
