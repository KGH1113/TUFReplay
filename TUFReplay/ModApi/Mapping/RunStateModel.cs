using System;
using TUFReplay.ModApi.Models;

namespace TUFReplay.ModApi.Mapping;

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

  /// <summary>Maps a state type; throws <see cref="RunStateModelException"/> if it can't be stored.</summary>
  public static RunStateModel For(Type type) => For(type, "state", "sealed class MyState { public int Count; }");

  /// <summary>Maps an event type; throws <see cref="RunStateModelException"/> if it can't be stored.</summary>
  public static RunStateModel ForEvent(Type type) =>
    For(type, "event", "sealed class KeyChanged { public int Index; public KeyCode Key; }");

  private static RunStateModel For(Type type, string role, string example)
  {
    string problem =
      "The " + role + " type is " + RunStateTypeNames.Of(type) + ". Use a class with fields, such as: " + example;
    if (type.IsValueType || type == typeof(string) || type.IsArray || type.IsInterface || type.IsAbstract)
      throw new RunStateModelException(problem);
    RunStateType mapped = RunStateTypeBuilder.Build(type);
    if (mapped.Shape.Kind != RunStateKind.Object)
      throw new RunStateModelException(problem);
    return new RunStateModel(mapped);
  }
}
