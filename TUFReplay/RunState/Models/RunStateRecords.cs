namespace TUFReplay.RunState.Models;

/// <summary>A captured state waiting to be saved with its run.</summary>
internal sealed class RunStateRecord
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;
  public string SavedAtUtc;
}

/// <summary>A state read back from the database for a replay.</summary>
internal sealed class StoredRunState
{
  public string StateId;
  public string ShapeId;
  public string ShapeJson;
  public string ValueJson;
}

/// <summary>One shape a state id was saved with, and how many runs use it.</summary>
internal sealed class StoredRunStateShape
{
  public string ShapeId;
  public string ShapeJson;
  public int RunCount;
}
