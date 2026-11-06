using System;

namespace TUFReplay.RunState.Models;

internal enum RunStateKind
{
  Boolean,
  Int32,
  Int64,
  Float32,
  Float64,
  String,
  Enum,
  DateTime,
  Bytes,
  List,
  Map,
  Object,
}

/// <summary>
/// The stored form of a mod's state type: the kind of each value and the names it is saved
/// under. Every saved value keeps the shape it was written with, so a later version of the mod
/// can tell exactly what an old run holds.
/// </summary>
internal sealed class RunStateShape
{
  public RunStateKind Kind;

  /// <summary>Whether null can be stored. Always true for strings, byte arrays, lists, dictionaries and classes.</summary>
  public bool Nullable;

  /// <summary>List items or dictionary values.</summary>
  public RunStateShape Element;

  /// <summary>Object members, sorted by ordinal name.</summary>
  public RunStateField[] Fields = Array.Empty<RunStateField>();

  /// <summary>Enum member names, sorted by ordinal name.</summary>
  public string[] EnumNames = Array.Empty<string>();

  public RunStateField FindField(string name)
  {
    foreach (RunStateField field in Fields)
      if (string.Equals(field.Name, name, StringComparison.Ordinal))
        return field;
    return null;
  }

  public RunStateShape AsNullable()
  {
    return new RunStateShape
    {
      Kind = Kind,
      Nullable = true,
      Element = Element,
      Fields = Fields,
      EnumNames = EnumNames,
    };
  }
}

internal sealed class RunStateField
{
  public string Name;
  public RunStateShape Shape;
}
