using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Shared.NativeInput;

namespace TUFReplay.Replay.NativeInput;

internal sealed class ReplayNativeInputPump : IDisposable
{
  private const long ClockDiscontinuityUs = 50_000L;
  private const int MaxCatchUpGroupsBeforeYield = 256;

  // There is only one OS keyboard state. A replacement replay must not press
  // keys until the previous worker has released its in-flight inputs. Only
  // workers wait for this handoff; Unity can install/dispose players immediately.
  private static readonly object HandoffGate = new object();
  private static Task _previousShutdown = Task.CompletedTask;
  private Task _predecessor;
  private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(
    TaskCreationOptions.RunContinuationsAsynchronously
  );

  // This gate protects only the mailbox and published statistics. Never hold it
  // while calling the emitter: Windows hooks may need Unity's message loop.
  private readonly object _gate = new object();
  private readonly Queue<Command> _commands = new Queue<Command>();
  private Command? _synchronization;
  private bool _shutdownRequested;
  private long _generation;
  private long _appliedGeneration;
  private long _snapshotGeneration;
  private ReplayNativeInputStats _snapshot;
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
    PublishSnapshot();
    _thread = new Thread(Run)
    {
      IsBackground = true,
      Name = "TUFReplay Native Input Pump",
      Priority = ThreadPriority.AboveNormal,
    };
    lock (HandoffGate)
    {
      _predecessor = _previousShutdown;
      _previousShutdown = _completion.Task;
    }
    try
    {
      _thread.Start();
    }
    catch
    {
      _waiter.Dispose();
      _wake.Dispose();
      _completion.TrySetResult(true);
      throw;
    }
  }

  public ReplayNativeInputStats Snapshot
  {
    get
    {
      lock (_gate)
        return _snapshot;
    }
  }

  public bool Finished
  {
    get
    {
      lock (_gate)
        return _snapshotGeneration == _generation && _snapshot.NextIndex >= _snapshot.Count;
    }
  }

  private void PublishSnapshot()
  {
    var snapshot = new ReplayNativeInputStats(
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
    lock (_gate)
    {
      _snapshot = snapshot;
      _snapshotGeneration = _appliedGeneration;
    }
  }

  public void ResetTo(long nowUs, double timelineRate, bool canEmit) =>
    Post(CommandKind.ResetTo, nowUs, timelineRate, canEmit);

  public void Synchronize(long nowUs, double timelineRate, bool canEmit) =>
    Post(CommandKind.Synchronize, nowUs, timelineRate, canEmit);

  public void Reset() => Post(CommandKind.Reset);

  public void SuspendAt(long nowUs) => Post(CommandKind.Suspend, nowUs);

  public void ReleaseAll() => Post(CommandKind.Release);

  public void Dispose() => Post(CommandKind.Shutdown);

  private void Post(CommandKind kind, long nowUs = 0, double timelineRate = 1d, bool canEmit = false)
  {
    long ticks = _clock.Timestamp;
    lock (_gate)
    {
      if (_shutdownRequested)
        return;

      // Discontinuities invalidate any unsent part of an in-flight batch. Clock
      // heartbeats alone must preserve overdue events, including after a stall.
      if (kind != CommandKind.Synchronize || !canEmit)
        Interlocked.Increment(ref _generation);
      var command = new Command(kind, nowUs, ticks, timelineRate, canEmit, _generation);
      if (kind == CommandKind.Shutdown)
      {
        _shutdownRequested = true;
        _commands.Clear();
        _synchronization = null;
        _commands.Enqueue(command);
      }
      else
      {
        // Coalesce frame heartbeats, but preserve focus-loss/resume boundaries
        // and the ordering of heartbeats relative to explicit transport commands.
        if (_synchronization.HasValue && (kind != CommandKind.Synchronize || _synchronization.Value.CanEmit != canEmit))
        {
          _commands.Enqueue(_synchronization.Value);
          _synchronization = null;
        }
        if (kind == CommandKind.Synchronize)
          _synchronization = command;
        else
          _commands.Enqueue(command);
      }
      _wake.Set();
    }
  }

  private bool TryTakeCommand(out Command command)
  {
    lock (_gate)
    {
      if (_commands.Count > 0)
      {
        command = _commands.Dequeue();
        return true;
      }
      if (_synchronization.HasValue)
      {
        command = _synchronization.Value;
        _synchronization = null;
        return true;
      }
      command = default;
      return false;
    }
  }

  private void ApplyCommand(Command command)
  {
    _appliedGeneration = command.Generation;
    switch (command.Kind)
    {
      case CommandKind.ResetTo:
        ReleaseHeldKeys();
        SeekAndAnchor(command.TimeUs, command.Ticks, command.Rate, command.CanEmit);
        _resumePending = !command.CanEmit;
        break;
      case CommandKind.Synchronize:
        if (!command.CanEmit)
          Suspend(command.TimeUs);
        else if (_resumePending)
        {
          SeekAndAnchor(command.TimeUs, command.Ticks, command.Rate, true);
          _resumePending = false;
        }
        else if (_active)
        {
          long predictedUs = PredictReplayTime(command.Ticks);
          if (Math.Abs(command.TimeUs - predictedUs) >= ClockDiscontinuityUs)
            _clockResidualCorrections++;
          _anchorReplayUs = command.TimeUs;
          _anchorTicks = command.Ticks;
          _timelineRate = NormalizeRate(command.Rate);
        }
        break;
      case CommandKind.Suspend:
        Suspend(command.TimeUs);
        break;
      case CommandKind.Reset:
        ReleaseHeldKeys();
        _scheduler.Reset();
        _active = false;
        _resumePending = false;
        break;
      case CommandKind.Release:
      case CommandKind.Shutdown:
        ReleaseHeldKeys();
        _active = false;
        _resumePending = false;
        _shutdown = command.Kind == CommandKind.Shutdown;
        break;
    }
  }

  private void Suspend(long nowUs)
  {
    int before = _scheduler.NextIndex;
    ReleaseHeldKeys();
    _scheduler.SeekToNativeState(nowUs);
    _explicitSeekSkipped += Math.Max(0, _scheduler.NextIndex - before);
    _active = false;
    _resumePending = true;
  }

  private void Run()
  {
    try
    {
      _predecessor.GetAwaiter().GetResult();
      _predecessor = null;
      int consecutiveCatchUpGroups = 0;
      while (!_shutdown)
      {
        if (TryTakeCommand(out Command command))
        {
          ApplyCommand(command);
          PublishSnapshot();
          continue;
        }
        bool waitInactive = false;
        bool yieldAfterCatchUp = false;
        long deadlineTicks = 0L;
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
            deadlineTicks = ReplayTimeToTicks(next.Value.TimeUs);
            long remainingTicks = deadlineTicks - nowTicks;
            if (remainingTicks <= 0)
            {
              long latenessUs = TicksToMicroseconds(-remainingTicks);
              long scheduledBefore = _scheduled;
              EmitNextGroup(latenessUs);
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
        PublishSnapshot();
        if (yieldAfterCatchUp)
          Thread.Yield();
        else if (waitInactive)
          _wake.WaitOne();
        else if (deadlineTicks > 0L)
          WaitForDeadline(deadlineTicks);
      }
    }
    catch (Exception)
    {
      // A failed native emitter must not crash Unity's process.
      _emissionFailures++;
    }
    finally
    {
      lock (_gate)
      {
        _shutdownRequested = true;
        _commands.Clear();
        _synchronization = null;
      }
      try
      {
        ReleaseHeldKeys();
      }
      catch (Exception)
      {
        _emissionFailures++;
      }
      PublishSnapshot();
      // Only the worker disposes wait handles, after its last native call. The
      // Unity thread must keep pumping messages even while shutdown is pending.
      try
      {
        _waiter.Dispose();
      }
      finally
      {
        _wake.Dispose();
        _completion.TrySetResult(true);
      }
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
      _waiter.Dispose();
      _waiter = new ManagedReplayDeadlineWaiter(_waiter.Name + ": " + exception.Message);
      _waiterFallbacks++;
      _wake.Set();
    }
  }

  private int SeekAndAnchor(long nowUs, long ticks, double timelineRate, bool emitState)
  {
    int before = _scheduler.NextIndex;
    List<NativeInputKey> targetHeldKeys = _scheduler.SeekToNativeState(nowUs);
    _explicitSeekSkipped += Math.Max(0, _scheduler.NextIndex - before);
    int changed = emitState ? EmitStateDelta(targetHeldKeys) : 0;
    _anchorReplayUs = nowUs;
    _anchorTicks = ticks;
    _timelineRate = NormalizeRate(timelineRate);
    _active = emitState && !_scheduler.Finished;
    _stateSeeks++;
    _wake.Set();
    return changed;
  }

  private int EmitNextGroup(long latenessUs)
  {
    if (!IsCurrentGeneration)
      return 0;
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
    int emitted = EmitWithRetry(count);
    ApplyEmittedState(emitted);
    if (latenessUs > _maxLatenessUs)
      _maxLatenessUs = latenessUs;
    return emitted;
  }

  private int EmitStateDelta(List<NativeInputKey> targetHeldKeys)
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
    int emitted = EmitWithRetry(count);
    ApplyEmittedState(emitted);
    return emitted;
  }

  private void ReleaseHeldKeys()
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
    int emitted = EmitWithRetry(count, releasing: true);
    ApplyEmittedState(emitted);
  }

  private bool IsCurrentGeneration => _appliedGeneration == Volatile.Read(ref _generation);

  private int EmitWithRetry(int count, bool releasing = false)
  {
    if (!releasing && !IsCurrentGeneration)
      return 0;
    NativeInputEmitResult first = _emitter.EmitBatch(_emissions, 0, count);
    int emitted = Math.Max(0, Math.Min(count, first.Emitted));
    if (emitted < count)
    {
      _emissionFailures++;
      // A pause/seek/stop may arrive while SendInput is in flight. Account for
      // its accepted prefix, but never retry stale key-downs after that barrier.
      if (releasing || IsCurrentGeneration)
      {
        _partialRetries++;
        NativeInputEmitResult retry = _emitter.EmitBatch(_emissions, emitted, count - emitted);
        emitted += Math.Max(0, Math.Min(count - emitted, retry.Emitted));
        if (emitted < count)
        {
          _emissionFailures++;
          _failedEvents += count - emitted;
        }
      }
    }
    _emitted += emitted;
    CountEmissionMetadata(emitted);
    return emitted;
  }

  private void ApplyEmittedState(int count)
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

  private long ReplayTimeToTicks(long replayTimeUs)
  {
    double deltaUs = (replayTimeUs - _anchorReplayUs) / _timelineRate;
    return _anchorTicks + (long)(deltaUs * _clock.Frequency / 1_000_000d);
  }

  private void CountEmissionMetadata(int count)
  {
    for (int i = 0; i < count; i++)
    {
      if (_emissions[i].NativeCode >= 0)
        _nativeMetadataEmitted++;
      else
        _fallbackEmitted++;
    }
  }

  private long PredictReplayTime(long ticks)
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

  private enum CommandKind
  {
    ResetTo,
    Synchronize,
    Reset,
    Suspend,
    Release,
    Shutdown,
  }

  private readonly struct Command
  {
    public readonly CommandKind Kind;
    public readonly long TimeUs;
    public readonly long Ticks;
    public readonly double Rate;
    public readonly bool CanEmit;
    public readonly long Generation;

    public Command(CommandKind kind, long timeUs, long ticks, double rate, bool canEmit, long generation)
    {
      Kind = kind;
      TimeUs = timeUs;
      Ticks = ticks;
      Rate = rate;
      CanEmit = canEmit;
      Generation = generation;
    }
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
