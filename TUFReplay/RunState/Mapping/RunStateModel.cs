using System;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Mapping;

/// <summary>A state type together with its shape, canonical shape JSON and shape id.</summary>
internal sealed class RunStateModel
{
  public readonly RunStateType Type;
  public readonly string ShapeJson;
  public readonly string ShapeId;

  private RunStateModel(RunStateType type)
  {
    Type = type;
    ShapeJson = RunStateShapeJson.Write(type.Shape);
    ShapeId = RunStateShapeJson.IdOf(ShapeJson);
  }

  public Type ClrType => Type.ClrType;
  public RunStateShape Shape => Type.Shape;

  /// <summary>Maps <paramref name="type"/>; throws <see cref="RunStateModelException"/> if it can't be stored.</summary>
  public static RunStateModel For(Type type)
  {
    if (type.IsValueType || type == typeof(string) || type.IsArray || type.IsInterface || type.IsAbstract)
      throw new RunStateModelException(
        "The state type is "
          + RunStateTypeNames.Of(type)
          + ". Use a class with fields, such as: sealed class MyState { public int Count; }"
      );
    RunStateType mapped = RunStateTypeBuilder.Build(type);
    if (mapped.Shape.Kind != RunStateKind.Object)
      throw new RunStateModelException(
        "The state type is "
          + RunStateTypeNames.Of(type)
          + ". Use a class with fields, such as: sealed class MyState { public int Count; }"
      );
    return new RunStateModel(mapped);
  }
}
