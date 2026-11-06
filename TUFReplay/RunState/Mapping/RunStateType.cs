using System;
using System.Reflection;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>
/// How one CLR type maps to a <see cref="RunStateShape"/>, with what is needed to read and
/// write its values. Built once per type when a provider registers.
/// </summary>
internal sealed class RunStateType
{
  public Type ClrType;
  public RunStateShape Shape;

  /// <summary>For <c>Nullable&lt;T&gt;</c>: the mapping of <c>T</c>.</summary>
  public RunStateType Underlying;

  /// <summary>List items or dictionary values.</summary>
  public RunStateType Element;

  /// <summary>True when a list is stored in a <c>T[]</c> rather than a <c>List&lt;T&gt;</c>.</summary>
  public bool IsArray;

  /// <summary>Object members, in the same order as <see cref="RunStateShape.Fields"/>.</summary>
  public RunStateMember[] Members = Array.Empty<RunStateMember>();

  /// <summary>Creates an empty object, list or dictionary.</summary>
  public object CreateInstance() => Activator.CreateInstance(ClrType, nonPublic: true);
}

internal sealed class RunStateMember
{
  public string Name;
  public RunStateType Type;
  public FieldInfo Field;
  public PropertyInfo Property;

  public object GetValue(object owner) => Field != null ? Field.GetValue(owner) : Property.GetValue(owner);

  public void SetValue(object owner, object value)
  {
    if (Field != null)
      Field.SetValue(owner, value);
    else
      Property.SetValue(owner, value);
  }
}
