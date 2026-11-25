using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.ModApi.Diagnostics;
using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;
using TUFReplay.Shared.Unity;

namespace TUFReplay.ModApi.Recording;

/// <summary>
/// What providers save with one recorded run. Providers are asked for their start value when the
/// run's input capture starts; a provider that returns a Task may finish any time before the run
/// is saved, and a later result is dropped. While the run records, providers add events through
/// <see cref="ReplayEventStore.AddEvent"/>, stamped with the run's timeline. Each value is written
/// to JSON as soon as it arrives, so changing the object afterwards does not change what is saved.
/// </summary>
internal sealed class RunStateCapture
{
  /// <summary>A capture slower than this on the main thread is reported once per provider.</summary>
  internal static readonly long SlowCaptureTicks = Stopwatch.Frequency / 1000;

  /// <summary>Events one provider can add during one run; later ones are dropped.</summary>
  internal const int MaxEventsPerRun = 1000;

  /// <summary>UTF-8 JSON bytes of events one provider can add during one run; later ones are dropped.</summary>
  internal const int MaxEventBytesPerRun = 1024 * 1024;

  private const int MaxReportedFailures = 3;

  private static RunStateCapture _current;

  private readonly object _gate = new object();
  private readonly Dictionary<string, RunStateRecord> _records = new Dictionary<string, RunStateRecord>(
    StringComparer.Ordinal
  );
  private readonly List<RunEventRecord> _events = new List<RunEventRecord>();
  private readonly Dictionary<string, EventBudget> _budgets = new Dictionary<string, EventBudget>(
    StringComparer.Ordinal
  );
  private readonly Func<long> _timelineNowUs;
  private bool _closed;
  private int _pending;

  public readonly string RunId;

  private RunStateCapture(string runId, Func<long> timelineNowUs)
  {
    RunId = runId;
    _timelineNowUs = timelineNowUs;
  }

  /// <summary>The run recording now, or null.</summary>
  internal static RunStateCapture Current => Volatile.Read(ref _current);

  /// <summary>
  /// Asks every registered provider for its start value and takes events until the run is
  /// closed. Call on the Unity main thread. <paramref name="timelineNowUs"/> is the run's
  /// timeline now, on the main thread; a null clock adds events at 0.
  /// </summary>
  public static RunStateCapture Begin(string runId, Func<long> timelineNowUs = null)
  {
    var capture = new RunStateCapture(runId, timelineNowUs);
    Interlocked.Exchange(ref _current, capture);
    foreach (ReplayProvider provider in ReplayProviders.All)
      if (provider.Captures)
        capture.CaptureFrom(provider);
    return capture;
  }

  /// <summary>Async captures still running, for tests.</summary>
  internal int PendingCount => Volatile.Read(ref _pending);

  /// <summary>Ends the capture and returns what arrived. Later results are dropped.</summary>
  public CapturedRunStates Close()
  {
    Release();
    lock (_gate)
    {
      _closed = true;
      return new CapturedRunStates(_records.Values.ToArray(), _events.ToArray());
    }
  }

  /// <summary>Ends the capture of a run that will not be saved.</summary>
  public void Discard()
  {
    Release();
    lock (_gate)
    {
      _closed = true;
      _records.Clear();
      _events.Clear();
    }
  }

  private void Release() => Interlocked.CompareExchange(ref _current, null, this);

  /// <summary>
  /// Adds <paramref name="e"/> for the provider registered under <paramref name="id"/>, at the
  /// current time on the run's timeline. False when no run records or the event can't be saved;
  /// reasons other than "no run" are written to the log.
  /// </summary>
  internal static bool AddEvent(string id, object e)
  {
    RunStateCapture capture = Current;
    if (capture == null)
      return false;
    ReplayProvider provider = ReplayProviders.Find(id);
    string problem = EventProblem(id, provider, e, out RunEventHandler handler);
    if (problem != null)
    {
      ReportEventProblemOnce(id, problem);
      return false;
    }
    return capture.Add(provider, handler, e);
  }

  private static string EventProblem(string id, ReplayProvider provider, object e, out RunEventHandler handler)
  {
    const string Name = "AddEvent";
    handler = null;
    if (!UnityMainThread.IsMainThread)
      return Name + " was called off Unity's main thread. Call it on the main thread.";
    if (provider == null)
      return Name + " found no provider registered under \"" + id + "\". Register it first.";
    if (!provider.TakesEvents)
      return Name
        + " needs "
        + ReplayProvider.EventName
        + "(YourEvent e, long ticks) on "
        + provider.ProviderTypeName
        + ", so the event can come back in a replay.";
    if (e == null)
      return Name + " got null. Pass an event the provider takes: " + EventNames(provider) + ".";
    handler = provider.EventFor(e.GetType());
    if (handler == null)
      return Name
        + " got "
        + RunStateTypeNames.Of(e.GetType())
        + ", but "
        + provider.ProviderTypeName
        + " has no "
        + ReplayProvider.EventName
        + "("
        + RunStateTypeNames.Of(e.GetType())
        + " e, long ticks). It takes "
        + EventNames(provider)
        + ".";
    return null;
  }

  private static string EventNames(ReplayProvider provider) =>
    string.Join(", ", provider.Events.Select(handler => handler.Name));

  private bool Add(ReplayProvider provider, RunEventHandler handler, object e)
  {
    long started = Stopwatch.GetTimestamp();
    string json;
    try
    {
      json = RunStateValueWriter.Write(handler.Model.Type, e);
    }
    catch (RunStateValueException exception)
    {
      ReportEventProblemOnce(provider.Id, handler.Name + " was not saved: " + exception.Message);
      return false;
    }
    finally
    {
      ReportIfSlow(
        provider,
        Stopwatch.GetTimestamp() - started,
        "AddEvent took {0} ms on the main thread. Add smaller events so ADOFAI does not stutter."
      );
    }

    long timeUs = _timelineNowUs?.Invoke() ?? 0L;
    lock (_gate)
    {
      if (_closed)
        return false;
      if (!_budgets.TryGetValue(provider.Id, out EventBudget budget))
        _budgets[provider.Id] = budget = new EventBudget();
      if (budget.Count >= MaxEventsPerRun || budget.Bytes + json.Length > MaxEventBytesPerRun)
      {
        if (!budget.ReportedFull)
        {
          budget.ReportedFull = true;
          ModApiLog.Write(
            "[ReplayApi] "
              + provider.Id
              + ": this run already has "
              + budget.Count
              + " events, the most one run keeps ("
              + MaxEventsPerRun
              + " events or "
              + MaxEventBytesPerRun / 1024
              + " KiB). Later events are not saved. Add an event when something changes, not every frame."
          );
        }
        return false;
      }

      // Every event is kept, even several at one moment. A clock that steps back counts as the
      // same moment, so one provider's events stay in the order they were added.
      if (timeUs < budget.LastTimeUs)
        timeUs = budget.LastTimeUs;
      _events.Add(
        new RunEventRecord
        {
          StateId = provider.Id,
          EventType = handler.SavedName,
          ShapeId = handler.Model.ShapeId,
          ShapeJson = handler.Model.ShapeJson,
          ValueJson = json,
          TimeUs = timeUs,
        }
      );
      budget.LastTimeUs = timeUs;
      budget.Count++;
      budget.Bytes += json.Length;
      return true;
    }
  }

  private void CaptureFrom(ReplayProvider provider)
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
      ReportIfSlow(
        provider,
        Stopwatch.GetTimestamp() - started,
        provider.ProviderTypeName
          + "."
          + ReplayProvider.CaptureName
          + " took {0} ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter."
      );
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

  private void Complete(ReplayProvider provider, Task task)
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

  private void Store(ReplayProvider provider, object value)
  {
    if (value == null)
      return;
    lock (_gate)
      if (_closed)
      {
        ModApiLog.Write(
          "[ReplayApi] "
            + provider.Id
            + ": "
            + ReplayProvider.CaptureName
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
      ModApiLog.Write("[ReplayApi] " + provider.Id + " was not saved for this run: " + exception.Message);
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

  private static void ReportFailure(ReplayProvider provider, Exception exception)
  {
    if (exception == null || Interlocked.Increment(ref provider.ReportedCaptureFailures) > MaxReportedFailures)
      return;
    ModApiLog.ProviderFailed(provider, ReplayProvider.CaptureName, exception);
  }

  /// <summary>Reports one slow main-thread call per provider. <paramref name="message"/> takes the milliseconds as {0}.</summary>
  private static void ReportIfSlow(ReplayProvider provider, long elapsedTicks, string message)
  {
    if (elapsedTicks <= SlowCaptureTicks || provider.WarnedSlowCapture)
      return;
    provider.WarnedSlowCapture = true;
    string milliseconds = (elapsedTicks * 1000.0 / Stopwatch.Frequency).ToString("0.0", CultureInfo.InvariantCulture);
    ModApiLog.Write(
      "[ReplayApi] " + provider.Id + ": " + string.Format(CultureInfo.InvariantCulture, message, milliseconds)
    );
  }

  private static readonly HashSet<string> ReportedEventProblems = new HashSet<string>(StringComparer.Ordinal);

  private static void ReportEventProblemOnce(string id, string problem)
  {
    lock (ReportedEventProblems)
      if (!ReportedEventProblems.Add((id ?? "") + "\n" + problem))
        return;
    ModApiLog.Write("[ReplayApi] " + (string.IsNullOrEmpty(id) ? "A provider" : id) + ": " + problem);
  }

  internal static void ResetForTests()
  {
    Interlocked.Exchange(ref _current, null);
    lock (ReportedEventProblems)
      ReportedEventProblems.Clear();
  }

  private sealed class EventBudget
  {
    public long LastTimeUs;
    public int Count;
    public int Bytes;
    public bool ReportedFull;
  }
}
