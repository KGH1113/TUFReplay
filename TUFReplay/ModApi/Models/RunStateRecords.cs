using System;
using System.Collections.Generic;

namespace TUFReplay.ModApi.Models;

/// <summary>A captured state waiting to be saved with its run.</summary>
internal sealed class RunStateRecord
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;
  public string SavedAtUtc;
}

/// <summary>A value a mod recorded during a run, waiting to be saved with it.</summary>
internal sealed class RunEventRecord
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;

  /// <summary>On the run's timeline, the same clock as its inputs and hits.</summary>
  public long TimeUs;
}

/// <summary>What the providers saved for one run: start values and values recorded during it.</summary>
internal sealed class CapturedRunStates
{
  public static readonly CapturedRunStates Empty = new CapturedRunStates(
    Array.Empty<RunStateRecord>(),
    Array.Empty<RunEventRecord>()
  );

  public readonly IReadOnlyList<RunStateRecord> States;

  /// <summary>In the order they were recorded, so by time.</summary>
  public readonly IReadOnlyList<RunEventRecord> Events;

  public CapturedRunStates(IReadOnlyList<RunStateRecord> states, IReadOnlyList<RunEventRecord> events)
  {
    States = states ?? Array.Empty<RunStateRecord>();
    Events = events ?? Array.Empty<RunEventRecord>();
  }

  public bool IsEmpty => States.Count == 0 && Events.Count == 0;
}

/// <summary>A state read back from the database for a replay.</summary>
internal sealed class StoredRunState
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;
}

/// <summary>A value recorded during a run, read back for a replay.</summary>
internal sealed class StoredRunEvent
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;
  public long TimeUs;
}

/// <summary>Everything the providers saved for one run, read back for a replay.</summary>
internal sealed class StoredRunStates
{
  public static readonly StoredRunStates Empty = new StoredRunStates(
    Array.Empty<StoredRunState>(),
    Array.Empty<StoredRunEvent>()
  );

  public readonly IReadOnlyList<StoredRunState> States;

  /// <summary>By time, then in the order they were recorded.</summary>
  public readonly IReadOnlyList<StoredRunEvent> Events;

  public StoredRunStates(IReadOnlyList<StoredRunState> states, IReadOnlyList<StoredRunEvent> events)
  {
    States = states ?? Array.Empty<StoredRunState>();
    Events = events ?? Array.Empty<StoredRunEvent>();
  }
}

/// <summary>One shape a state id was saved with, and how many runs use it.</summary>
internal sealed class StoredRunStateShape
{
  public string ShapeId;
  public string ShapeJson;
  public int RunCount;
}
