using System;
using System.Collections.Generic;
using System.Linq;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>
/// Decides whether a value saved with one shape can be read into another type without help.
/// Added members keep their initial value, removed members are skipped, and numbers may widen
/// (int to long or double, float to double). Anything else needs an upgrade method.
/// </summary>
internal sealed class RunStateCompatibility
{
  /// <summary>Why the value can't be read automatically, or null when it can.</summary>
  public string Problem { get; private set; }

  /// <summary>Saved members the target no longer has.</summary>
  public readonly List<string> Dropped = new List<string>();

  /// <summary>Target members the saved value lacks; they keep their initial value.</summary>
  public readonly List<string> Defaulted = new List<string>();

  public bool IsReadable => Problem == null;

  public static RunStateCompatibility Check(RunStateShape saved, RunStateShape target, string rootName)
  {
    var result = new RunStateCompatibility();
    result.Problem = result.Compare(saved, target, rootName);
    return result;
  }

  private string Compare(RunStateShape saved, RunStateShape target, string path)
  {
    if (saved.Kind != target.Kind && !Widens(saved.Kind, target.Kind))
      return path + " was " + RunStateTypeNames.Of(saved) + " and is now " + RunStateTypeNames.Of(target) + ".";

    if (saved.Nullable && !target.Nullable)
      return path
        + " was "
        + RunStateTypeNames.Of(saved)
        + " and is now "
        + RunStateTypeNames.Of(target)
        + ", so a saved null would have nowhere to go.";

    if (saved.Kind != target.Kind)
      return null;

    switch (saved.Kind)
    {
      case RunStateKind.Enum:
        string[] removed = saved.EnumNames.Except(target.EnumNames, StringComparer.Ordinal).ToArray();
        return removed.Length == 0
          ? null
          : path + " was saved with enum members that no longer exist: " + string.Join(", ", removed) + ".";
      case RunStateKind.List:
      case RunStateKind.Map:
        return Compare(saved.Element, target.Element, path + "[]");
      case RunStateKind.Object:
        foreach (RunStateField field in target.Fields)
        {
          RunStateField old = saved.FindField(field.Name);
          if (old == null)
          {
            Defaulted.Add(path + "." + field.Name);
            continue;
          }
          string problem = Compare(old.Shape, field.Shape, path + "." + field.Name);
          if (problem != null)
            return problem;
        }
        foreach (RunStateField field in saved.Fields)
          if (target.FindField(field.Name) == null)
            Dropped.Add(path + "." + field.Name);
        return null;
      default:
        return null;
    }
  }

  internal static bool Widens(RunStateKind from, RunStateKind to) =>
    (from, to)
      is
        (RunStateKind.Int32, RunStateKind.Int64)
        or
        (RunStateKind.Int32, RunStateKind.Float64)
        or
        (RunStateKind.Float32, RunStateKind.Float64);
}
