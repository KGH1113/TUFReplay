using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.RunState.Diagnostics;
using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Models;
using TUFReplay.RunState.Providers;

namespace TUFReplay.RunState.Recording;

/// <summary>
/// The states captured for one recorded run. Providers are asked when the run's input capture
/// starts. A provider that returns a Task may finish any time before the run is saved; a later
/// result is dropped. Each value is written to JSON as soon as it arrives, so changing the
/// object afterwards does not change what is saved.
/// </summary>
internal sealed class RunStateCapture
{
  /// <summary>A capture slower than this on the main thread is reported once per provider.</summary>
  internal static readonly long SlowCaptureTicks = Stopwatch.Frequency / 1000;

  private const int MaxReportedFailures = 3;

  private readonly object _gate = new object();
  private readonly Dictionary<string, RunStateRecord> _records = new Dictionary<string, RunStateRecord>(
    StringComparer.Ordinal
  );
  private bool _closed;
  private int _pending;

  public readonly string RunId;

  private RunStateCapture(string runId)
  {
    RunId = runId;
  }

  /// <summary>Asks every registered provider for its state. Call on the Unity main thread.</summary>
  public static RunStateCapture Begin(string runId)
  {
    var capture = new RunStateCapture(runId);
    foreach (RunStateProvider provider in RunStateProviders.All)
      if (provider.Captures)
        capture.CaptureFrom(provider);
    return capture;
  }

  /// <summary>Async captures still running, for tests.</summary>
  internal int PendingCount => Volatile.Read(ref _pending);

  /// <summary>Ends the capture and returns what arrived. Later results are dropped.</summary>
  public IReadOnlyList<RunStateRecord> Close()
  {
    lock (_gate)
    {
      _closed = true;
      return _records.Values.ToArray();
    }
  }

  /// <summary>Ends the capture of a run that will not be saved.</summary>
  public void Discard()
  {
    lock (_gate)
    {
      _closed = true;
      _records.Clear();
    }
  }

  private void CaptureFrom(RunStateProvider provider)
  {
    long started = Stopwatch.GetTimestamp();
    object result;
    try
    {
      result = provider.InvokeCapture();
    }
    catch (Exception exception)
    {
      ReportFailure(provider, exception);
      return;
    }
    finally
    {
      ReportIfSlow(provider, Stopwatch.GetTimestamp() - started);
    }

    if (!provider.CapturesAsync)
    {
      Store(provider, result);
      return;
    }
    if (!(result is Task task))
      return;
    if (task.IsCompleted)
    {
      Complete(provider, task);
      return;
    }
    Interlocked.Increment(ref _pending);
    task.ContinueWith(
      finished =>
      {
        try
        {
          Complete(provider, finished);
        }
        finally
        {
          Interlocked.Decrement(ref _pending);
        }
      },
      CancellationToken.None,
      TaskContinuationOptions.None,
      TaskScheduler.Default
    );
  }

  private void Complete(RunStateProvider provider, Task task)
  {
    if (task.IsCanceled)
      return;
    if (task.IsFaulted)
    {
      ReportFailure(provider, task.Exception?.GetBaseException());
      return;
    }
    Store(provider, provider.ReadCaptureResult(task));
  }

  private void Store(RunStateProvider provider, object value)
  {
    if (value == null)
      return;
    lock (_gate)
      if (_closed)
      {
        RunStateLog.Write(
          "[RunState] "
            + provider.Id
            + ": "
            + RunStateProvider.CaptureName
            + " finished after the run ended, so its value was not saved for that run."
        );
        return;
      }

    string json;
    try
    {
      json = RunStateValueWriter.Write(provider.Model.Type, value);
    }
    catch (RunStateValueException exception)
    {
      RunStateLog.Write("[RunState] " + provider.Id + " was not saved for this run: " + exception.Message);
      return;
    }

    var record = new RunStateRecord
    {
      StateId = provider.Id,
      ShapeId = provider.Model.ShapeId,
      ShapeJson = provider.Model.ShapeJson,
      ValueJson = json,
      SavedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
    };
    lock (_gate)
      if (!_closed)
        _records[provider.Id] = record;
  }

  private static void ReportFailure(RunStateProvider provider, Exception exception)
  {
    if (exception == null || Interlocked.Increment(ref provider.ReportedCaptureFailures) > MaxReportedFailures)
      return;
    RunStateLog.ProviderFailed(provider, RunStateProvider.CaptureName, exception);
  }

  private static void ReportIfSlow(RunStateProvider provider, long elapsedTicks)
  {
    if (elapsedTicks <= SlowCaptureTicks || provider.WarnedSlowCapture)
      return;
    provider.WarnedSlowCapture = true;
    RunStateLog.Write(
      "[RunState] "
        + provider.Id
        + ": "
        + provider.ProviderTypeName
        + "."
        + RunStateProvider.CaptureName
        + " took "
        + (elapsedTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture)
        + " ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter."
    );
  }
}
