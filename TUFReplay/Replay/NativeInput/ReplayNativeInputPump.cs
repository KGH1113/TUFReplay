using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Replay.NativeInput;

internal sealed class ReplayNativeInputPump : IDisposable
{
  private const long ClockDiscontinuityUs = 50_000L;
  private const int MaxCatchUpGroupsBeforeYield = 256;

  private readonly object _gate = new object();
  private readonly ReplayInputScheduler _scheduler;
  private readonly INativeInputEmitter _emitter;
  private readonly AutoResetEvent _wake = new AutoResetEvent(false);
  private readonly Thread _thread;
  private readonly IReplayMonotonicClock _clock;
  private readonly List<RecordedInput> _group = new List<RecordedInput>();
  private readonly HashSet<NativeInputKey> _heldKeys = new HashSet<NativeInputKey>();

  private NativeInputEmission[] _emissions = new NativeInputEmission[32];
  private bool _active;
  private bool _resumePending;
  private bool _shutdown;
  private long _anchorReplayUs;
  private long _anchorTicks;
  private double _timelineRate = 1d;
  private long _emitted;
  private long _stateSeeks;
  private long _emissionFailures;
  private long _maxLatenessUs;
  private long _nativeMetadataEmitted;
  private long _fallbackEmitted;
  private long _scheduled;
  private long _catchUpGroups;
  private long _catchUpEvents;
  private long _partialRetries;
  private long _failedEvents;
  private long _unsupportedEvents;
  private long _explicitSeekSkipped;
  private long _clockResidualCorrections;
  private long _waiterFallbacks;
  private readonly ReplayLatenessHistogram _lateness = new ReplayLatenessHistogram();
  private IReplayDeadlineWaiter _waiter;

  public ReplayNativeInputPump(ReplayInputScheduler scheduler, INativeInputEmitter emitter)
    : this(scheduler, emitter, StopwatchReplayMonotonicClock.Instance, ReplayDeadlineWaiterFactory.Create()) { }

  internal ReplayNativeInputPump(
    ReplayInputScheduler scheduler,
    INativeInputEmitter emitter,
    IReplayMonotonicClock clock,
    IReplayDeadlineWaiter waiter
  )
  {
    _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
    _thread = new Thread(Run)
    {
      IsBackground = true,
      Name = "TUFReplay Native Input Pump",
      Priority = ThreadPriority.AboveNormal,
    };
    _thread.Start();
  }

  public ReplayNativeInputStats Snapshot
  {
    get
    {
      lock (_gate)
      {
        return new ReplayNativeInputStats(
          _emitted,
          _stateSeeks,
          _emissionFailures,
          _maxLatenessUs,
          _nativeMetadataEmitted,
          _fallbackEmitted,
          _scheduled,
          _catchUpGroups,
          _catchUpEvents,
          _partialRetries,
          _failedEvents,
          _unsupportedEvents,
          _explicitSeekSkipped,
          _clockResidualCorrections,
          _waiterFallbacks,
          _lateness.Percentile(0.50d),
          _lateness.Percentile(0.95d),
          _lateness.Percentile(0.99d),
          _waiter.Name,
          _waiter.FallbackReason,
          _scheduler.NextIndex,
          _scheduler.Count
        );
      }
    }
  }

  public int ResetTo(long nowUs, double timelineRate, bool canEmit)
  {
    lock (_gate)
    {
      ReleaseAllLocked();
      int changed = SeekAndAnchorLocked(nowUs, timelineRate, canEmit);
      _resumePending = !canEmit;
      return changed;
    }
  }

  public int Synchronize(long nowUs, double timelineRate, bool canEmit)
  {
    lock (_gate)
    {
      if (_shutdown)
        return 0;

      if (!canEmit)
      {
        int before = _scheduler.NextIndex;
        ReleaseAllLocked();
        _scheduler.SeekToNativeState(nowUs);
        _explicitSeekSkipped += Math.Max(0, _scheduler.NextIndex - before);
        _active = false;
        _resumePending = true;
        _wake.Set();
        return Math.Max(0, _scheduler.NextIndex - before);
      }

      if (!_active)
      {
        if (!_resumePending)
          return 0;
        int changed = SeekAndAnchorLocked(nowUs, timelineRate, true);
        _resumePending = false;
        return changed;
      }

      long ticks = _clock.Timestamp;
      long predictedUs = PredictReplayTimeLocked(ticks);
      if (Math.Abs(nowUs - predictedUs) >= ClockDiscontinuityUs)
        _clockResidualCorrections++;

      _anchorReplayUs = nowUs;
      _anchorTicks = ticks;
      _timelineRate = NormalizeRate(timelineRate);
      _wake.Set();
      return 0;
    }
  }

  public void Reset()
  {
    lock (_gate)
    {
      ReleaseAllLocked();
      _scheduler.Reset();
      _active = false;
      _resumePending = false;
      _wake.Set();
    }
  }

  public int SuspendAt(long nowUs)
  {
    lock (_gate)
    {
      int before = _scheduler.NextIndex;
      ReleaseAllLocked();
      _scheduler.SeekToNativeState(nowUs);
      _explicitSeekSkipped += Math.Max(0, _scheduler.NextIndex - before);
      _active = false;
      _resumePending = true;
      _wake.Set();
      return Math.Max(0, _scheduler.NextIndex - before);
    }
  }

  public void ReleaseAll()
  {
    lock (_gate)
    {
      ReleaseAllLocked();
      _active = false;
      _resumePending = false;
      _wake.Set();
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_shutdown)
        return;
      ReleaseAllLocked();
      _active = false;
      _resumePending = false;
      _shutdown = true;
      _wake.Set();
    }

    if (Thread.CurrentThread != _thread)
      _thread.Join(500);
    _waiter.Dispose();
    _wake.Dispose();
  }

  private void Run()
  {
    int consecutiveCatchUpGroups = 0;
    while (true)
    {
      bool waitInactive = false;
      bool yieldAfterCatchUp = false;
      long deadlineTicks = 0L;
      lock (_gate)
      {
        if (_shutdown)
          return;

        if (!_active)
        {
          waitInactive = true;
          consecutiveCatchUpGroups = 0;
        }
        else
        {
          RecordedInput? next = _scheduler.PeekNext();
          if (!next.HasValue)
          {
            _active = false;
            waitInactive = true;
            consecutiveCatchUpGroups = 0;
          }
          else
          {
            long nowTicks = _clock.Timestamp;
            deadlineTicks = ReplayTimeToTicksLocked(next.Value.TimeUs);
            long remainingTicks = deadlineTicks - nowTicks;
            if (remainingTicks <= 0)
            {
              long latenessUs = TicksToMicroseconds(-remainingTicks);
              long scheduledBefore = _scheduled;
              EmitNextGroupLocked(latenessUs);
              _lateness.Record(latenessUs, _scheduled - scheduledBefore);
              if (latenessUs > 0L)
              {
                _catchUpGroups++;
                _catchUpEvents += _scheduled - scheduledBefore;
              }
              consecutiveCatchUpGroups++;
              yieldAfterCatchUp = consecutiveCatchUpGroups >= MaxCatchUpGroupsBeforeYield;
              if (yieldAfterCatchUp)
                consecutiveCatchUpGroups = 0;
              deadlineTicks = 0L;
            }
            else
              consecutiveCatchUpGroups = 0;
          }
        }
      }

      if (yieldAfterCatchUp)
        Thread.Yield();
      else if (waitInactive)
        _wake.WaitOne();
      else if (deadlineTicks > 0L)
        WaitForDeadline(deadlineTicks);
    }
  }

  private void WaitForDeadline(long deadlineTicks)
  {
    try
    {
      _waiter.WaitUntil(deadlineTicks, _wake, _clock);
    }
    catch (Exception exception)
    {
      lock (_gate)
      {
        _waiter.Dispose();
        _waiter = new ManagedReplayDeadlineWaiter(_waiter.Name + ": " + exception.Message);
        _waiterFallbacks++;
      }
      _wake.Set();
    }
  }

  private int SeekAndAnchorLocked(long nowUs, double timelineRate, bool emitState)
  {
    int before = _scheduler.NextIndex;
    List<NativeInputKey> targetHeldKeys = _scheduler.SeekToNativeState(nowUs);
    _explicitSeekSkipped += Math.Max(0, _scheduler.NextIndex - before);
    int changed = emitState ? EmitStateDeltaLocked(targetHeldKeys) : 0;
    if (!emitState)
      _heldKeys.Clear();

    _anchorReplayUs = nowUs;
    _anchorTicks = _clock.Timestamp;
    _timelineRate = NormalizeRate(timelineRate);
    _active = emitState && !_scheduler.Finished;
    _stateSeeks++;
    _wake.Set();
    return changed;
  }

  private int EmitNextGroupLocked(long latenessUs)
  {
    if (_scheduler.CopyNextTimestampGroup(_group) == 0)
      return 0;

    int count = 0;
    for (int i = 0; i < _group.Count; i++)
    {
      RecordedInput input = _group[i];
      if (!input.Async)
        continue;
      if (!_emitter.IsSupported(input.Key))
      {
        _unsupportedEvents++;
        continue;
      }
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(
        input.Key,
        input.Down,
        input.ExtendedKey,
        input.NativeCode,
        input.NativeFlags
      );
    }

    if (count == 0)
      return 0;
    _scheduled += count;
    int emitted = EmitWithRetryLocked(count);
    ApplyEmittedStateLocked(emitted);
    if (latenessUs > _maxLatenessUs)
      _maxLatenessUs = latenessUs;
    return emitted;
  }

  private int EmitStateDeltaLocked(List<NativeInputKey> targetHeldKeys)
  {
    HashSet<NativeInputKey> target = new HashSet<NativeInputKey>(targetHeldKeys);
    int count = 0;

    foreach (NativeInputKey heldKey in _heldKeys)
    {
      if (target.Contains(heldKey) || !_emitter.IsSupported(heldKey.Key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(
        heldKey.Key,
        false,
        heldKey.ExtendedKey,
        heldKey.NativeCode,
        heldKey.NativeFlags
      );
    }

    for (int i = 0; i < targetHeldKeys.Count; i++)
    {
      NativeInputKey key = targetHeldKeys[i];
      if (_heldKeys.Contains(key) || !_emitter.IsSupported(key.Key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(key.Key, true, key.ExtendedKey, key.NativeCode, key.NativeFlags);
    }

    if (count == 0)
    {
      _heldKeys.Clear();
      foreach (NativeInputKey key in target)
      {
        if (_emitter.IsSupported(key.Key))
          _heldKeys.Add(key);
      }
      return 0;
    }

    _scheduled += count;
    int emitted = EmitWithRetryLocked(count);
    ApplyEmittedStateLocked(emitted);
    return emitted;
  }

  private void ReleaseAllLocked()
  {
    if (_heldKeys.Count == 0)
      return;

    int count = 0;
    foreach (NativeInputKey key in _heldKeys)
    {
      if (!_emitter.IsSupported(key.Key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(key.Key, false, key.ExtendedKey, key.NativeCode, key.NativeFlags);
    }

    if (count == 0)
      return;
    _scheduled += count;
    int emitted = EmitWithRetryLocked(count);
    ApplyEmittedStateLocked(emitted);
  }

  private int EmitWithRetryLocked(int count)
  {
    NativeInputEmitResult first = _emitter.EmitBatch(_emissions, 0, count);
    int emitted = Math.Max(0, Math.Min(count, first.Emitted));
    if (emitted < count)
    {
      _emissionFailures++;
      _partialRetries++;
      NativeInputEmitResult retry = _emitter.EmitBatch(_emissions, emitted, count - emitted);
      emitted += Math.Max(0, Math.Min(count - emitted, retry.Emitted));
      if (emitted < count)
      {
        _emissionFailures++;
        _failedEvents += count - emitted;
      }
    }
    _emitted += emitted;
    CountEmissionMetadataLocked(emitted);
    return emitted;
  }

  private void ApplyEmittedStateLocked(int count)
  {
    for (int i = 0; i < count; i++)
    {
      NativeInputEmission emission = _emissions[i];
      NativeInputKey key = new NativeInputKey(
        emission.Key,
        emission.ExtendedKey,
        emission.NativeCode,
        emission.NativeFlags
      );
      if (emission.Down)
        _heldKeys.Add(key);
      else
        _heldKeys.Remove(key);
    }
  }

  private long ReplayTimeToTicksLocked(long replayTimeUs)
  {
    double deltaUs = (replayTimeUs - _anchorReplayUs) / _timelineRate;
    return _anchorTicks + (long)(deltaUs * _clock.Frequency / 1_000_000d);
  }

  private void CountEmissionMetadataLocked(int count)
  {
    for (int i = 0; i < count; i++)
    {
      if (_emissions[i].NativeCode >= 0)
        _nativeMetadataEmitted++;
      else
        _fallbackEmitted++;
    }
  }

  private long PredictReplayTimeLocked(long ticks)
  {
    double elapsedUs = (ticks - _anchorTicks) * 1_000_000d / _clock.Frequency;
    return _anchorReplayUs + (long)(elapsedUs * _timelineRate);
  }

  private long TicksToMicroseconds(long ticks)
  {
    return (long)(ticks * 1_000_000d / _clock.Frequency);
  }

  private static double NormalizeRate(double rate)
  {
    return rate > 0d && !double.IsNaN(rate) && !double.IsInfinity(rate) ? rate : 1d;
  }

  private void EnsureEmissionCapacity(int count)
  {
    if (_emissions.Length >= count)
      return;
    int capacity = _emissions.Length;
    while (capacity < count)
      capacity *= 2;
    _emissions = new NativeInputEmission[capacity];
  }
}

public readonly struct ReplayNativeInputStats
{
  public readonly long Emitted;
  public readonly long StateSeeks;
  public readonly long EmissionFailures;
  public readonly long MaxLatenessUs;
  public readonly long NativeMetadataEmitted;
  public readonly long FallbackEmitted;
  public readonly long Scheduled;
  public readonly long CatchUpGroups;
  public readonly long CatchUpEvents;
  public readonly long PartialRetries;
  public readonly long FailedEvents;
  public readonly long UnsupportedEvents;
  public readonly long ExplicitSeekSkipped;
  public readonly long ClockResidualCorrections;
  public readonly long WaiterFallbacks;
  public readonly long P50LatenessUs;
  public readonly long P95LatenessUs;
  public readonly long P99LatenessUs;
  public readonly string Waiter;
  public readonly string WaiterFallbackReason;
  public readonly int NextIndex;
  public readonly int Count;

  public ReplayNativeInputStats(
    long emitted,
    long stateSeeks,
    long emissionFailures,
    long maxLatenessUs,
    long nativeMetadataEmitted,
    long fallbackEmitted,
    long scheduled,
    long catchUpGroups,
    long catchUpEvents,
    long partialRetries,
    long failedEvents,
    long unsupportedEvents,
    long explicitSeekSkipped,
    long clockResidualCorrections,
    long waiterFallbacks,
    long p50LatenessUs,
    long p95LatenessUs,
    long p99LatenessUs,
    string waiter,
    string waiterFallbackReason,
    int nextIndex,
    int count
  )
  {
    Emitted = emitted;
    StateSeeks = stateSeeks;
    EmissionFailures = emissionFailures;
    MaxLatenessUs = maxLatenessUs;
    NativeMetadataEmitted = nativeMetadataEmitted;
    FallbackEmitted = fallbackEmitted;
    Scheduled = scheduled;
    CatchUpGroups = catchUpGroups;
    CatchUpEvents = catchUpEvents;
    PartialRetries = partialRetries;
    FailedEvents = failedEvents;
    UnsupportedEvents = unsupportedEvents;
    ExplicitSeekSkipped = explicitSeekSkipped;
    ClockResidualCorrections = clockResidualCorrections;
    WaiterFallbacks = waiterFallbacks;
    P50LatenessUs = p50LatenessUs;
    P95LatenessUs = p95LatenessUs;
    P99LatenessUs = p99LatenessUs;
    Waiter = waiter;
    WaiterFallbackReason = waiterFallbackReason;
    NextIndex = nextIndex;
    Count = count;
  }
}
