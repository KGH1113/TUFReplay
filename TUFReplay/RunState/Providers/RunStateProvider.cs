using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Models;

namespace TUFReplay.RunState.Providers;

/// <summary>
/// A registered provider object and the contract methods found on it by name:
/// <c>CaptureRunState</c>, <c>OnReplayBegin</c>, <c>OnReplayEnd</c> and <c>UpgradeRunState</c>.
/// Binding checks that they agree on one state type and that the type can be stored.
/// </summary>
internal sealed class RunStateProvider
{
  internal const string CaptureName = "CaptureRunState";
  internal const string BeginName = "OnReplayBegin";
  internal const string EndName = "OnReplayEnd";
  internal const string UpgradeName = "UpgradeRunState";

  public readonly string Id;
  public readonly object Instance;
  public readonly RunStateModel Model;

  private readonly MethodInfo _capture;
  private readonly PropertyInfo _captureResult;
  private readonly MethodInfo _begin;
  private readonly MethodInfo _end;
  private readonly RunStateUpgrade[] _upgrades;
  private readonly ConcurrentDictionary<string, RunStateReadPlan> _plans = new ConcurrentDictionary<
    string,
    RunStateReadPlan
  >(StringComparer.Ordinal);

  internal bool WarnedSlowCapture;
  internal int ReportedCaptureFailures;

  private RunStateProvider(
    string id,
    object instance,
    RunStateModel model,
    MethodInfo capture,
    MethodInfo begin,
    MethodInfo end,
    RunStateUpgrade[] upgrades
  )
  {
    Id = id;
    Instance = instance;
    Model = model;
    _capture = capture;
    _begin = begin;
    _end = end;
    _upgrades = upgrades;
    if (capture != null && IsTaskOfState(capture.ReturnType))
      _captureResult = capture.ReturnType.GetProperty("Result");
  }

  public bool Captures => _capture != null;
  public bool CapturesAsync => _captureResult != null;
  public bool FollowsReplays => _begin != null || _end != null;
  public string ProviderTypeName => RunStateTypeNames.Of(Instance.GetType());

  public static RunStateProvider Bind(string id, object instance)
  {
    Type type = instance.GetType();
    string typeName = RunStateTypeNames.Of(type);
    MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    MethodInfo capture = Single(methods, CaptureName, typeName);
    MethodInfo begin = Single(methods, BeginName, typeName);
    MethodInfo end = Single(methods, EndName, typeName);

    if (capture == null && begin == null)
    {
      string found = string.Join(
        ", ",
        methods.Where(method => method.DeclaringType != typeof(object)).Select(method => method.Name).Distinct()
      );
      throw new RunStateModelException(
        typeName
          + " has neither "
          + CaptureName
          + " nor "
          + BeginName
          + ". Add at least one of them as a public method. Public methods found: "
          + (found.Length == 0 ? "none" : found)
          + "."
      );
    }

    Type captured = capture == null ? null : CapturedType(capture, typeName);
    Type replayed = begin == null ? null : ReplayedType(begin, typeName);
    if (captured != null && replayed != null && captured != replayed)
      throw new RunStateModelException(
        typeName
          + "."
          + CaptureName
          + " returns "
          + RunStateTypeNames.Of(captured)
          + " but "
          + BeginName
          + " takes "
          + RunStateTypeNames.Of(replayed)
          + ". Use the same state type in both."
      );
    if (end != null && end.GetParameters().Length != 0)
      throw new RunStateModelException(typeName + "." + EndName + " must take no parameters.");

    RunStateModel model = RunStateModel.For(captured ?? replayed);
    RunStateUpgrade[] upgrades = BindUpgrades(methods, model, typeName);
    return new RunStateProvider(id, instance, model, capture, begin, end, upgrades);
  }

  private static MethodInfo Single(MethodInfo[] methods, string name, string typeName)
  {
    MethodInfo[] found = methods.Where(method => method.Name == name).ToArray();
    if (found.Length > 1)
      throw new RunStateModelException(typeName + " has " + found.Length + " " + name + " methods. Keep one.");
    return found.Length == 1 ? found[0] : null;
  }

  private static Type CapturedType(MethodInfo capture, string typeName)
  {
    if (capture.GetParameters().Length != 0)
      throw new RunStateModelException(typeName + "." + CaptureName + " must take no parameters.");
    Type returned = capture.ReturnType;
    if (IsTaskOfState(returned))
      return returned.GetGenericArguments()[0];
    if (returned == typeof(void) || typeof(Task).IsAssignableFrom(returned))
      throw new RunStateModelException(
        typeName + "." + CaptureName + " must return the state, or a Task<state> when it finishes later."
      );
    return returned;
  }

  private static Type ReplayedType(MethodInfo begin, string typeName)
  {
    ParameterInfo[] parameters = begin.GetParameters();
    if (parameters.Length == 1 || (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool)))
      return parameters[0].ParameterType;
    throw new RunStateModelException(
      typeName + "." + BeginName + " must take (YourState recorded) or (YourState recorded, bool render)."
    );
  }

  private static bool IsTaskOfState(Type type) =>
    type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);

  private static RunStateUpgrade[] BindUpgrades(MethodInfo[] methods, RunStateModel model, string typeName)
  {
    var upgrades = new List<RunStateUpgrade>();
    foreach (MethodInfo method in methods.Where(method => method.Name == UpgradeName))
    {
      ParameterInfo[] parameters = method.GetParameters();
      if (parameters.Length != 1 || method.ReturnType != model.ClrType)
        throw new RunStateModelException(
          typeName
            + "."
            + UpgradeName
            + " must take one older state and return "
            + RunStateTypeNames.Of(model.ClrType)
            + ", such as: "
            + RunStateTypeNames.Of(model.ClrType)
            + " "
            + UpgradeName
            + "(MyStateV1 old)."
        );
      Type source = parameters[0].ParameterType;
      RunStateModel sourceModel;
      try
      {
        sourceModel = RunStateModel.For(source);
      }
      catch (RunStateModelException exception)
      {
        throw new RunStateModelException(
          typeName + "." + UpgradeName + "(" + RunStateTypeNames.Of(source) + "): " + exception.Message
        );
      }
      if (sourceModel.ShapeId == model.ShapeId)
        throw new RunStateModelException(
          typeName
            + "."
            + UpgradeName
            + " takes "
            + RunStateTypeNames.Of(source)
            + ", which has the same members as "
            + RunStateTypeNames.Of(model.ClrType)
            + ", so it can never be used. Give the old type the members it was saved with."
        );
      if (upgrades.Any(upgrade => upgrade.Source.ShapeId == sourceModel.ShapeId))
        throw new RunStateModelException(
          typeName + " has two " + UpgradeName + " methods for types with the same members. Keep one."
        );
      upgrades.Add(new RunStateUpgrade(sourceModel, method));
    }
    return upgrades.ToArray();
  }

  public object InvokeCapture() => Invoke(_capture, null);

  public object ReadCaptureResult(Task task) => _captureResult.GetValue(task);

  public void InvokeBegin(object recorded, bool render)
  {
    if (_begin == null)
      return;
    Invoke(_begin, _begin.GetParameters().Length == 2 ? new[] { recorded, render } : new[] { recorded });
  }

  public void InvokeEnd()
  {
    if (_end != null)
      Invoke(_end, null);
  }

  private object Invoke(MethodInfo method, object[] arguments)
  {
    try
    {
      return method.Invoke(Instance, arguments);
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
      throw new RunStateProviderException(exception.InnerException);
    }
  }

  /// <summary>How a value saved with <paramref name="shapeId"/> is read. Cached per shape.</summary>
  public RunStateReadPlan PlanFor(string shapeId, string shapeJson)
  {
    return _plans.GetOrAdd(shapeId, _ => CreatePlan(shapeId, shapeJson));
  }

  private RunStateReadPlan CreatePlan(string shapeId, string shapeJson)
  {
    if (shapeId == Model.ShapeId)
      return RunStateReadPlan.Direct(null);
    foreach (RunStateUpgrade upgrade in _upgrades)
      if (shapeId == upgrade.Source.ShapeId)
        return RunStateReadPlan.ViaUpgrade(upgrade, null);

    RunStateShape saved;
    try
    {
      saved = RunStateShapeJson.Parse(shapeJson);
    }
    catch (Exception exception)
    {
      return RunStateReadPlan.Unreadable("its saved shape is damaged (" + exception.Message + ").");
    }

    RunStateCompatibility direct = RunStateCompatibility.Check(saved, Model.Shape, RunStateTypeNames.Of(Model.ClrType));
    if (direct.IsReadable)
      return RunStateReadPlan.Direct(direct);
    foreach (RunStateUpgrade upgrade in _upgrades)
    {
      RunStateCompatibility viaUpgrade = RunStateCompatibility.Check(
        saved,
        upgrade.Source.Shape,
        RunStateTypeNames.Of(upgrade.Source.ClrType)
      );
      if (viaUpgrade.IsReadable)
        return RunStateReadPlan.ViaUpgrade(upgrade, viaUpgrade);
    }
    return RunStateReadPlan.Unreadable(direct.Problem);
  }

  /// <summary>Reads a saved value into the current state type, upgrading it when the plan says so.</summary>
  public object Read(RunStateReadPlan plan, string valueJson)
  {
    if (plan.Upgrade == null)
      return RunStateValueReader.Read(Model.Type, valueJson);
    object old = RunStateValueReader.Read(plan.Upgrade.Source.Type, valueJson);
    return Invoke(plan.Upgrade.Method, new[] { old });
  }
}

internal sealed class RunStateUpgrade
{
  public readonly RunStateModel Source;
  public readonly MethodInfo Method;

  public RunStateUpgrade(RunStateModel source, MethodInfo method)
  {
    Source = source;
    Method = method;
  }
}

internal sealed class RunStateReadPlan
{
  public RunStateUpgrade Upgrade { get; private set; }

  /// <summary>Why the value can't be read, or null.</summary>
  public string Problem { get; private set; }

  /// <summary>The automatic conversion used, or null for an exact shape.</summary>
  public RunStateCompatibility Conversion { get; private set; }

  public bool IsReadable => Problem == null;

  public static RunStateReadPlan Direct(RunStateCompatibility conversion) =>
    new RunStateReadPlan { Conversion = conversion };

  public static RunStateReadPlan ViaUpgrade(RunStateUpgrade upgrade, RunStateCompatibility conversion) =>
    new RunStateReadPlan { Upgrade = upgrade, Conversion = conversion };

  public static RunStateReadPlan Unreadable(string problem) => new RunStateReadPlan { Problem = problem };
}

/// <summary>An exception thrown by the mod's own provider method.</summary>
internal sealed class RunStateProviderException : Exception
{
  public RunStateProviderException(Exception inner)
    : base(inner.Message, inner) { }
}
