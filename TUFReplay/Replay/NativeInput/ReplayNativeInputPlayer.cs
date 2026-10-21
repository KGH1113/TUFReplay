using System;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;

namespace TUFReplay.Replay.NativeInput;

public sealed class ReplayNativeInputPlayer : IDisposable
{
  private readonly INativeInputFocusGuard _focusGuard;
  private readonly ReplayNativeInputPump _pump;
  private readonly string _emitterName;
  private long _lastReportedEmitted;
  private bool? _lastFocusReady;
  private string _lastFocusReason;

  internal ReplayNativeInputPlayer(
    ReplayInputScheduler scheduler,
    INativeInputEmitter emitter,
    INativeInputFocusGuard focusGuard
  )
  {
    _focusGuard = focusGuard ?? throw new System.ArgumentNullException(nameof(focusGuard));
    _pump = new ReplayNativeInputPump(scheduler, emitter ?? throw new ArgumentNullException(nameof(emitter)));
    _emitterName = emitter.GetType().Name;
  }

  public bool Finished => _pump.Finished;
  public ReplayNativeInputStats Stats => _pump.Snapshot;

  public void Reset()
  {
    _pump.Reset();
    _lastReportedEmitted = _pump.Snapshot.Emitted;
  }

  public void ResetTo(long nowUs, double timelineRate)
  {
    bool focusReady = CanEmit(out _);
    _pump.ResetTo(nowUs, timelineRate, focusReady);
    _lastReportedEmitted = _pump.Snapshot.Emitted;
  }

  public void ResetTo(ReplayPlaybackSnapshot snapshot) => ResetTo(snapshot.TimelineTimeUs, snapshot.TimelineRate);

  public int Tick(long nowUs, double timelineRate)
  {
    bool focusReady = CanEmit(out _);
    _pump.Synchronize(nowUs, timelineRate, focusReady);
    ReplayNativeInputStats stats = _pump.Snapshot;
    long emitted = Math.Max(0L, stats.Emitted - _lastReportedEmitted);
    _lastReportedEmitted = stats.Emitted;
    return emitted > int.MaxValue ? int.MaxValue : (int)emitted;
  }

  public int Tick(ReplayPlaybackSnapshot snapshot) => Tick(snapshot.TimelineTimeUs, snapshot.TimelineRate);

  public bool CanEmit(out string reason)
  {
    bool ready = _focusGuard.IsStable(out reason);
    if (_lastFocusReady != ready || !string.Equals(_lastFocusReason, reason, StringComparison.Ordinal))
    {
      _lastFocusReady = ready;
      _lastFocusReason = reason;
      Main.Instance?.Log(
        "[Replay/Input] Focus "
          + (ready ? "ready" : "blocked")
          + ". reason="
          + (reason ?? "none")
          + ", emitter="
          + _emitterName
          + ", "
          + _focusGuard.Describe()
      );
    }
    return ready;
  }

  public void SkipTo(long nowUs)
  {
    _pump.SuspendAt(nowUs);
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
