using System;
using System.Linq;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>Type and shape names as C# authors write them, for messages.</summary>
internal static class RunStateTypeNames
{
  public static string Of(Type type)
  {
    if (type == null)
      return "null";
    Type underlying = Nullable.GetUnderlyingType(type);
    if (underlying != null)
      return Of(underlying) + "?";
    if (type.IsArray)
      return Of(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
    string alias = Alias(type);
    if (alias != null)
      return alias;
    if (!type.IsGenericType)
      return type.Name;
    string name = type.Name;
    int tick = name.IndexOf('`');
    if (tick >= 0)
      name = name.Substring(0, tick);
    return name + "<" + string.Join(", ", type.GetGenericArguments().Select(Of)) + ">";
  }

  public static string Of(RunStateShape shape)
  {
    string name = shape.Kind switch
    {
      RunStateKind.Boolean => "bool",
      RunStateKind.Int32 => "int",
      RunStateKind.Int64 => "long",
      RunStateKind.Float32 => "float",
      RunStateKind.Float64 => "double",
      RunStateKind.String => "string",
      RunStateKind.Enum => "an enum",
      RunStateKind.DateTime => "DateTime",
      RunStateKind.Bytes => "byte[]",
      RunStateKind.List => "a list",
      RunStateKind.Map => "a dictionary",
      _ => "an object",
    };
    return shape.Nullable && IsValueKind(shape.Kind) ? name + "?" : name;
  }

  internal static bool IsValueKind(RunStateKind kind) =>
    kind
      is RunStateKind.Boolean
        or RunStateKind.Int32
        or RunStateKind.Int64
        or RunStateKind.Float32
        or RunStateKind.Float64
        or RunStateKind.Enum
        or RunStateKind.DateTime;

  private static string Alias(Type type)
  {
    if (type == typeof(bool))
      return "bool";
    if (type == typeof(byte))
      return "byte";
    if (type == typeof(sbyte))
      return "sbyte";
    if (type == typeof(short))
      return "short";
    if (type == typeof(ushort))
      return "ushort";
    if (type == typeof(int))
      return "int";
    if (type == typeof(uint))
      return "uint";
    if (type == typeof(long))
      return "long";
    if (type == typeof(ulong))
      return "ulong";
    if (type == typeof(float))
      return "float";
    if (type == typeof(double))
      return "double";
    if (type == typeof(decimal))
      return "decimal";
    if (type == typeof(char))
      return "char";
    if (type == typeof(string))
      return "string";
    if (type == typeof(object))
      return "object";
    return null;
  }
}
