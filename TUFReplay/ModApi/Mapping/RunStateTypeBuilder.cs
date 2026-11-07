using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TUFReplay.ModApi.Models;

namespace TUFReplay.ModApi.Mapping;

/// <summary>
/// Maps a mod's state type to a <see cref="RunStateType"/>, or explains which member cannot be
/// stored. Stored members are public instance fields and public properties with a setter;
/// <c>[NonSerialized]</c> fields and get-only properties are left out.
/// </summary>
internal sealed class RunStateTypeBuilder
{
  internal const string SupportedTypes =
    "Use bool, int, long, float, double, string, an enum, DateTime, byte[], T[], List<T>, "
    + "Dictionary<string, T>, or a class or struct made of those.";

  private readonly Dictionary<Type, RunStateType> _built = new Dictionary<Type, RunStateType>();
  private readonly HashSet<Type> _building = new HashSet<Type>();

  public static RunStateType Build(Type type)
  {
    if (type == null)
      throw new ArgumentNullException(nameof(type));
    return new RunStateTypeBuilder().BuildType(type, RunStateTypeNames.Of(type));
  }

  private RunStateType BuildType(Type type, string path)
  {
    if (_building.Contains(type))
      throw Problem(path, type, "It contains itself. A run state can't nest a type inside itself; use a list instead.");
    if (_built.TryGetValue(type, out RunStateType built))
      return built;
    RunStateType result = BuildUncached(type, path);
    _built[type] = result;
    return result;
  }

  private RunStateType BuildUncached(Type type, string path)
  {
    if (type == typeof(bool))
      return Scalar(type, RunStateKind.Boolean, false);
    if (type == typeof(int))
      return Scalar(type, RunStateKind.Int32, false);
    if (type == typeof(long))
      return Scalar(type, RunStateKind.Int64, false);
    if (type == typeof(float))
      return Scalar(type, RunStateKind.Float32, false);
    if (type == typeof(double))
      return Scalar(type, RunStateKind.Float64, false);
    if (type == typeof(string))
      return Scalar(type, RunStateKind.String, true);
    if (type == typeof(DateTime))
      return Scalar(type, RunStateKind.DateTime, false);
    if (type == typeof(byte[]))
      return Scalar(type, RunStateKind.Bytes, true);
    if (type.IsEnum)
      return BuildEnum(type, path);

    Type underlying = Nullable.GetUnderlyingType(type);
    if (underlying != null)
    {
      RunStateType inner = BuildType(underlying, path);
      return new RunStateType
      {
        ClrType = type,
        Shape = inner.Shape.AsNullable(),
        Underlying = inner,
      };
    }

    if (type.IsArray)
    {
      if (type.GetArrayRank() != 1)
        throw Problem(path, type, "Use a jagged array (T[][]) or a list instead.");
      return BuildList(type, type.GetElementType(), isArray: true, path);
    }

    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
      return BuildList(type, type.GetGenericArguments()[0], isArray: false, path);

    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
    {
      Type[] arguments = type.GetGenericArguments();
      if (arguments[0] != typeof(string))
        throw Problem(path, type, "Dictionary keys must be strings. Use Dictionary<string, T>.");
      RunStateType values = BuildType(arguments[1], path + "[]");
      return new RunStateType
      {
        ClrType = type,
        Shape = new RunStateShape
        {
          Kind = RunStateKind.Map,
          Nullable = true,
          Element = values.Shape,
        },
        Element = values,
      };
    }

    RejectUnsupported(type, path);
    return BuildObject(type, path);
  }

  private static RunStateType Scalar(Type type, RunStateKind kind, bool nullable)
  {
    return new RunStateType
    {
      ClrType = type,
      Shape = new RunStateShape { Kind = kind, Nullable = nullable },
    };
  }

  private static RunStateType BuildEnum(Type type, string path)
  {
    if (type.IsDefined(typeof(FlagsAttribute), false))
      throw Problem(
        path,
        type,
        "It is a [Flags] enum, and values are stored by member name. Store a List<"
          + RunStateTypeNames.Of(type)
          + "> instead."
      );
    string[] names = Enum.GetNames(type);
    Array.Sort(names, StringComparer.Ordinal);
    return new RunStateType
    {
      ClrType = type,
      Shape = new RunStateShape { Kind = RunStateKind.Enum, EnumNames = names },
    };
  }

  private RunStateType BuildList(Type type, Type itemType, bool isArray, string path)
  {
    RunStateType items = BuildType(itemType, path + "[]");
    return new RunStateType
    {
      ClrType = type,
      IsArray = isArray,
      Shape = new RunStateShape
      {
        Kind = RunStateKind.List,
        Nullable = true,
        Element = items.Shape,
      },
      Element = items,
    };
  }

  private static void RejectUnsupported(Type type, string path)
  {
    if (type == typeof(object) || type.IsInterface || type.IsAbstract)
      throw Problem(path, type, "TUFReplay needs the exact type to read a run back. Use a concrete class or struct.");
    if (typeof(Delegate).IsAssignableFrom(type))
      throw Problem(path, type, "Delegates can't be stored.");
    if (typeof(UnityEngine.Object).IsAssignableFrom(type))
      throw Problem(path, type, "Unity objects can't be stored. Store the values you need from it instead.");
    if (type.IsPrimitive || type.IsPointer || type == typeof(decimal) || type.IsGenericTypeDefinition)
      throw Problem(path, type, SupportedTypes);
    if (type.IsGenericType || typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
      throw Problem(path, type, SupportedTypes);
  }

  private RunStateType BuildObject(Type type, string path)
  {
    if (!type.IsValueType && type.GetConstructor(MemberFlags, null, Type.EmptyTypes, null) == null)
      throw Problem(
        path,
        type,
        "It has no parameterless constructor, so TUFReplay can't create it when reading a run back. "
          + "Add one; it can be private."
      );
    _building.Add(type);
    try
    {
      var node = new RunStateType
      {
        ClrType = type,
        Shape = new RunStateShape { Kind = RunStateKind.Object, Nullable = !type.IsValueType },
      };

      var members = new List<RunStateMember>();
      foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
      {
        if (field.IsLiteral || field.IsNotSerialized)
          continue;
        string memberPath = path + "." + field.Name;
        if (field.IsInitOnly)
          throw new RunStateModelException(
            memberPath
              + " is readonly, so TUFReplay can't set it when reading a run back. "
              + "Remove readonly, or mark it [NonSerialized] to leave it out."
          );
        members.Add(
          new RunStateMember
          {
            Name = field.Name,
            Field = field,
            Type = BuildType(field.FieldType, memberPath),
          }
        );
      }
      foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.GetIndexParameters().Length != 0)
          continue;
        if (property.GetGetMethod(false) == null || property.GetSetMethod(true) == null)
          continue;
        members.Add(
          new RunStateMember
          {
            Name = property.Name,
            Property = property,
            Type = BuildType(property.PropertyType, path + "." + property.Name),
          }
        );
      }

      members.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
      for (int index = 1; index < members.Count; index++)
        if (members[index].Name == members[index - 1].Name)
          throw new RunStateModelException(
            path + " has two members named " + members[index].Name + ". Rename one of them."
          );

      node.Members = members.ToArray();
      node.Shape.Fields = members
        .Select(member => new RunStateField { Name = member.Name, Shape = member.Type.Shape })
        .ToArray();
      return node;
    }
    finally
    {
      _building.Remove(type);
    }
  }

  private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

  private static RunStateModelException Problem(string path, Type type, string fix)
  {
    return new RunStateModelException(path + " is " + RunStateTypeNames.Of(type) + ", which can't be stored. " + fix);
  }
}
