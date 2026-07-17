using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.NativeInput;

namespace TUFReplay.Features.Replay;

internal sealed class ReplayNativeInputPump : IDisposable
{
  private const long ClockDiscontinuityUs = 50_000L;
  private const int CoarseWaitThresholdMs = 2;

  private readonly object _gate = new object();
  private readonly ReplayInputScheduler _scheduler;
  private readonly INativeInputEmitter _emitter;
  private readonly AutoResetEvent _wake = new AutoResetEvent(false);
  private readonly Thread _thread;
  private readonly List<RecordedInput> _group = new List<RecordedInput>();
  private readonly HashSet<int> _heldKeys = new HashSet<int>();

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

  public ReplayNativeInputPump(ReplayInputScheduler scheduler, INativeInputEmitter emitter)
  {
    _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    _emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    _thread = new Thread(Run)
    {
      IsBackground = true,
      Name = "TUFReplay Native Input Pump",
      Priority = ThreadPriority.BelowNormal,
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
        _scheduler.SeekToState(nowUs);
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

      long ticks = Stopwatch.GetTimestamp();
      long predictedUs = PredictReplayTimeLocked(ticks);
      if (Math.Abs(nowUs - predictedUs) >= ClockDiscontinuityUs)
      {
        return SeekAndAnchorLocked(nowUs, timelineRate, true);
      }

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
      _scheduler.SeekToState(nowUs);
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
    _wake.Dispose();
  }

  private void Run()
  {
    while (true)
    {
      int waitMilliseconds;
      lock (_gate)
      {
        if (_shutdown)
          return;

        if (!_active)
        {
          waitMilliseconds = Timeout.Infinite;
        }
        else
        {
          RecordedInput? next = _scheduler.PeekNext();
          if (!next.HasValue)
          {
            _active = false;
            waitMilliseconds = Timeout.Infinite;
          }
          else
          {
            long nowTicks = Stopwatch.GetTimestamp();
            long deadlineTicks = ReplayTimeToTicksLocked(next.Value.TimeUs);
            long remainingTicks = deadlineTicks - nowTicks;
            if (remainingTicks <= 0)
            {
              long latenessUs = TicksToMicroseconds(-remainingTicks);
              if (latenessUs >= ClockDiscontinuityUs)
              {
                SeekAndAnchorLocked(PredictReplayTimeLocked(nowTicks), _timelineRate, true);
              }
              else
              {
                EmitNextGroupLocked(latenessUs);
              }
              continue;
            }

            long remainingMs = remainingTicks * 1000L / Stopwatch.Frequency;
            waitMilliseconds = remainingMs > CoarseWaitThresholdMs ? (int)Math.Min(remainingMs - 1L, 1000L) : 0;
          }
        }
      }

      if (waitMilliseconds == 0)
      {
        Thread.Yield();
      }
      else
      {
        _wake.WaitOne(waitMilliseconds);
      }
    }
  }

  private int SeekAndAnchorLocked(long nowUs, double timelineRate, bool emitState)
  {
    List<int> targetHeldKeys = _scheduler.SeekToState(nowUs);
    int changed = emitState ? EmitStateDeltaLocked(targetHeldKeys) : 0;
    if (!emitState)
      _heldKeys.Clear();

    _anchorReplayUs = nowUs;
    _anchorTicks = Stopwatch.GetTimestamp();
    _timelineRate = NormalizeRate(timelineRate);
    _active = emitState && !_scheduler.Finished;
    _stateSeeks++;
    _wake.Set();
    return changed;
  }

  private void EmitNextGroupLocked(long latenessUs)
  {
    if (_scheduler.CopyNextTimestampGroup(_group) == 0)
      return;

    int count = 0;
    for (int i = 0; i < _group.Count; i++)
    {
      RecordedInput input = _group[i];
      if (!input.Async || !_emitter.IsSupported(input.Key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(input.Key, input.Down);
    }

    if (count == 0)
      return;
    if (!_emitter.EmitBatch(_emissions, count))
    {
      _emissionFailures++;
      return;
    }

    for (int i = 0; i < count; i++)
    {
      NativeInputEmission emission = _emissions[i];
      if (emission.Down)
        _heldKeys.Add(emission.Key);
      else
        _heldKeys.Remove(emission.Key);
    }

    _emitted += count;
    if (latenessUs > _maxLatenessUs)
      _maxLatenessUs = latenessUs;
  }

  private int EmitStateDeltaLocked(List<int> targetHeldKeys)
  {
    HashSet<int> target = new HashSet<int>(targetHeldKeys);
    int count = 0;

    foreach (int heldKey in _heldKeys)
    {
      if (target.Contains(heldKey) || !_emitter.IsSupported(heldKey))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(heldKey, false);
    }

    for (int i = 0; i < targetHeldKeys.Count; i++)
    {
      int key = targetHeldKeys[i];
      if (_heldKeys.Contains(key) || !_emitter.IsSupported(key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(key, true);
    }

    if (count == 0)
    {
      _heldKeys.Clear();
      foreach (int key in target)
      {
        if (_emitter.IsSupported(key))
          _heldKeys.Add(key);
      }
      return 0;
    }

    if (!_emitter.EmitBatch(_emissions, count))
    {
      _emissionFailures++;
      return 0;
    }

    _heldKeys.Clear();
    foreach (int key in target)
    {
      if (_emitter.IsSupported(key))
        _heldKeys.Add(key);
    }
    _emitted += count;
    return count;
  }

  private void ReleaseAllLocked()
  {
    if (_heldKeys.Count == 0)
      return;

    int count = 0;
    foreach (int key in _heldKeys)
    {
      if (!_emitter.IsSupported(key))
        continue;
      EnsureEmissionCapacity(count + 1);
      _emissions[count++] = new NativeInputEmission(key, false);
    }

    if (count > 0 && _emitter.EmitBatch(_emissions, count))
      _emitted += count;
    else if (count > 0)
      _emissionFailures++;
    _heldKeys.Clear();
  }

  private long ReplayTimeToTicksLocked(long replayTimeUs)
  {
    double deltaUs = (replayTimeUs - _anchorReplayUs) / _timelineRate;
    return _anchorTicks + (long)(deltaUs * Stopwatch.Frequency / 1_000_000d);
  }

  private long PredictReplayTimeLocked(long ticks)
  {
    double elapsedUs = (ticks - _anchorTicks) * 1_000_000d / Stopwatch.Frequency;
    return _anchorReplayUs + (long)(elapsedUs * _timelineRate);
  }

  private static long TicksToMicroseconds(long ticks)
  {
    return (long)(ticks * 1_000_000d / Stopwatch.Frequency);
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
  public readonly int NextIndex;
  public readonly int Count;

  public ReplayNativeInputStats(
    long emitted,
    long stateSeeks,
    long emissionFailures,
    long maxLatenessUs,
    int nextIndex,
    int count
  )
  {
    Emitted = emitted;
    StateSeeks = stateSeeks;
    EmissionFailures = emissionFailures;
    MaxLatenessUs = maxLatenessUs;
    NextIndex = nextIndex;
    Count = count;
  }
}
