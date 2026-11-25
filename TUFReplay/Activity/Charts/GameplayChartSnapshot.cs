using System;
using System.Collections.Generic;

namespace TUFReplay.Activity.Charts;

/// <summary>
/// The parts of a level that the V5 gameplay hash reads, as plain values, so the
/// hash does not depend on ADOFAI types. GameplayChartSnapshotReader fills it in game.
/// </summary>
internal sealed class GameplayChartSnapshot
{
  public float Bpm;
  public int Offset;
  public bool SeparateCountdownTime;
  public int CountdownTicks;
  public bool LegacyPath;
  public string PathData;
  public IReadOnlyList<float> Angles;

  /// <summary>Events in file order, including inactive ones; the hasher skips those.</summary>
  public readonly List<GameplayChartItem> Events = new List<GameplayChartItem>();

  /// <summary>Decorations whose hitbox is Kill or Event, in file order.</summary>
  public readonly List<GameplayChartItem> HitboxDecorations = new List<GameplayChartItem>();
}

/// <summary>
/// One event or decoration. Values hold bool, int, float, string or float[] (vectors);
/// enums are their names. Absent lists properties the file left out, which only
/// MoveDecorations treats differently from their default.
/// </summary>
internal sealed class GameplayChartItem
{
  private Dictionary<string, object> _values;
  private HashSet<string> _absent;

  public GameplayChartItem(string type, int floor, bool active)
  {
    Type = type;
    Floor = floor;
    Active = active;
  }

  public string Type { get; }
  public int Floor { get; }
  public bool Active { get; }

  public GameplayChartItem Set(string property, object value)
  {
    (_values ??= new Dictionary<string, object>(StringComparer.Ordinal))[property] = value;
    return this;
  }

  public GameplayChartItem MarkAbsent(string property)
  {
    (_absent ??= new HashSet<string>(StringComparer.Ordinal)).Add(property);
    return this;
  }

  public object Get(string property) =>
    _values != null && _values.TryGetValue(property, out object value) ? value : null;

  public bool IsAbsent(string property) => _absent != null && _absent.Contains(property);
}
