using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Models;
using UnityEngine;

namespace TUFReplay.ModApi.Providers;

/// <summary>
/// A registered provider object and the <see cref="ReplayApi"/> members found on it by name.
/// Every member is optional, but a provider needs at least one. Members that run every frame
/// are bound once to delegates. The state type comes from <c>CaptureRunState</c>'s result or
/// <c>OnReplayEvent</c>'s value, which must be the same type.
/// </summary>
internal sealed class ReplayProvider
{
  internal const string CaptureName = "CaptureRunState";
  internal const string UpgradeName = "UpgradeRunState";
  internal const string BeginName = "OnReplayBegin";
  internal const string EventName = "OnReplayEvent";
  internal const string EndName = "OnReplayEnd";
  internal const string ClockName = "OnReplayClock";
  internal const string KeyName = "OnReplayKey";
  internal const string CanvasesName = "GetReplayCanvases";
  internal const string BeforeCaptureName = "OnBeforeFrameCapture";
  internal const string AfterCaptureName = "OnAfterFrameCapture";
  internal const string OverlayVideosName = "AddReplayOverlayVideos";

  private static readonly string[] KnownMembers =
  {
    CaptureName,
    UpgradeName,
    BeginName,
    EventName,
    EndName,
    ClockName,
    KeyName,
    CanvasesName,
    BeforeCaptureName,
    AfterCaptureName,
    OverlayVideosName,
  };

  public readonly string Id;
  public readonly object Instance;

  /// <summary>The state type saved with each run, or null for a provider that saves none.</summary>
  public readonly RunStateModel Model;

  private readonly MethodInfo _capture;
  private readonly PropertyInfo _captureResult;
  private readonly MethodInfo _begin;
  private readonly bool _beginTakesRender;
  private readonly MethodInfo _event;
  private readonly Action _end;
  private readonly RunStateUpgrade[] _upgrades;
  private readonly ConcurrentDictionary<string, RunStateReadPlan> _plans = new ConcurrentDictionary<
    string,
    RunStateReadPlan
  >(StringComparer.Ordinal);

  // Per-frame members, bound once.
  internal readonly Action<long> Clock;
  internal readonly Action<KeyCode, int?, bool, long> Key;
  internal readonly Func<IEnumerable<Canvas>> Canvases;
  internal readonly Func<Canvas> SingleCanvas;
  internal readonly Action BeforeCapture;
  internal readonly Action AfterCapture;

  /// <summary>Hands the provider an <c>add(path, source, target)</c> callback once, after a render's last frame.</summary>
  internal readonly Action<Action<string, RectInt, RectInt>> OverlayVideos;

  internal bool WarnedSlowCapture;
  internal int ReportedCaptureFailures;

  private ReplayProvider(
    string id,
    object instance,
    RunStateModel model,
    MethodInfo capture,
    MethodInfo begin,
    bool beginTakesRender,
    MethodInfo replayEvent,
    RunStateUpgrade[] upgrades,
    Bound bound
  )
  {
    Id = id;
    Instance = instance;
    Model = model;
    _capture = capture;
    _begin = begin;
    _beginTakesRender = beginTakesRender;
    _event = replayEvent;
    _upgrades = upgrades;
    if (capture != null && IsTaskOfState(capture.ReturnType))
      _captureResult = capture.ReturnType.GetProperty("Result");
    _end = bound.End;
    Clock = bound.Clock;
    Key = bound.Key;
    Canvases = bound.Canvases;
    SingleCanvas = bound.SingleCanvas;
    BeforeCapture = bound.BeforeCapture;
    AfterCapture = bound.AfterCapture;
    OverlayVideos = bound.OverlayVideos;
  }

  public bool Captures => _capture != null;
  public bool CapturesAsync => _captureResult != null;

  /// <summary>Takes the run's start value and recorded values back through <c>OnReplayEvent</c>.</summary>
  public bool TakesEvents => _event != null;

  public bool FollowsReplays =>
    _begin != null
    || _event != null
    || _end != null
    || Clock != null
    || Key != null
    || Canvases != null
    || SingleCanvas != null
    || BeforeCapture != null
    || AfterCapture != null
    || OverlayVideos != null;
  public bool ProvidesCanvases => Canvases != null || SingleCanvas != null;

  /// <summary>What the provider takes part in, for the registration log.</summary>
  public string Describe()
  {
    var parts = new List<string>();
    if (Model != null)
      parts.Add("state " + RunStateTypeNames.Of(Model.ClrType));
    if (_begin != null || _end != null)
      parts.Add("replay begin/end");
    if (_event != null)
      parts.Add("replay events");
    if (Key != null)
      parts.Add("keys");
    if (Clock != null)
      parts.Add("clock");
    if (ProvidesCanvases)
      parts.Add("render canvases");
    if (BeforeCapture != null || AfterCapture != null)
      parts.Add("frame capture");
    if (OverlayVideos != null)
      parts.Add("overlay videos");
    return string.Join(", ", parts);
  }

  public string ProviderTypeName => RunStateTypeNames.Of(Instance.GetType());

  public static ReplayProvider Bind(string id, object instance)
  {
    Type type = instance.GetType();
    string typeName = RunStateTypeNames.Of(type);
    MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    if (!methods.Any(method => KnownMembers.Contains(method.Name)))
    {
      string found = string.Join(
        ", ",
        methods.Where(method => method.DeclaringType != typeof(object)).Select(method => method.Name).Distinct()
      );
      throw new RunStateModelException(
        typeName
          + " has none of the ReplayApi methods ("
          + string.Join(", ", KnownMembers)
          + "). Add the ones your mod needs as public methods. Public methods found: "
          + (found.Length == 0 ? "none" : found)
          + "."
      );
    }

    MethodInfo capture = Single(methods, CaptureName, typeName);
    MethodInfo begin = Single(methods, BeginName, typeName);
    MethodInfo replayEvent = Single(methods, EventName, typeName);
    Type captured = capture == null ? null : CapturedType(capture, typeName);
    bool beginTakesRender = begin != null && BeginTakesRender(begin, typeName);
    Type replayed = replayEvent == null ? null : EventType(replayEvent, typeName);
    if (captured != null && replayed != null && captured != replayed)
      throw new RunStateModelException(
        typeName
          + "."
          + CaptureName
          + " returns "
          + RunStateTypeNames.Of(captured)
          + " but "
          + EventName
          + " takes "
          + RunStateTypeNames.Of(replayed)
          + ". Use the same state type in both."
      );

    Type stateType = captured ?? replayed;
    RunStateModel model = stateType == null ? null : RunStateModel.For(stateType);
    RunStateUpgrade[] upgrades = BindUpgrades(methods, model, typeName);
    Bound bound = BindReplayMembers(methods, instance, typeName);
    return new ReplayProvider(id, instance, model, capture, begin, beginTakesRender, replayEvent, upgrades, bound);
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

  private static bool BeginTakesRender(MethodInfo begin, string typeName)
  {
    ParameterInfo[] parameters = begin.GetParameters();
    if (begin.ReturnType == typeof(void))
    {
      if (parameters.Length == 0)
        return false;
      if (parameters.Length == 1 && parameters[0].ParameterType == typeof(bool))
        return true;
    }
    bool takesState = parameters.Length > 0 && parameters[0].ParameterType != typeof(bool);
    throw new RunStateModelException(
      typeName
        + "."
        + BeginName
        + " must be void "
        + BeginName
        + "() or void "
        + BeginName
        + "(bool render)."
        + (
          takesState
            ? " The run's state no longer comes with it: take it in void "
              + EventName
              + "("
              + RunStateTypeNames.Of(parameters[0].ParameterType)
              + " value, long ticks), which runs right after it."
            : ""
        )
    );
  }

  private static Type EventType(MethodInfo replayEvent, string typeName)
  {
    ParameterInfo[] parameters = replayEvent.GetParameters();
    if (
      replayEvent.ReturnType != typeof(void)
      || parameters.Length != 2
      || parameters[1].ParameterType != typeof(long)
      || parameters[0].ParameterType == typeof(long)
      || parameters[0].ParameterType == typeof(bool)
    )
      throw new RunStateModelException(
        typeName + "." + EventName + " must be void " + EventName + "(YourState value, long ticks)."
      );
    return parameters[0].ParameterType;
  }

  private static bool IsTaskOfState(Type type) =>
    type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);

  private static RunStateUpgrade[] BindUpgrades(MethodInfo[] methods, RunStateModel model, string typeName)
  {
    var upgrades = new List<RunStateUpgrade>();
    foreach (MethodInfo method in methods.Where(method => method.Name == UpgradeName))
    {
      if (model == null)
        throw new RunStateModelException(
          typeName
            + "."
            + UpgradeName
            + " has no state to upgrade to. Add "
            + CaptureName
            + " or "
            + EventName
            + "(YourState value, long ticks), or remove it."
        );
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

  private struct Bound
  {
    public Action End;
    public Action<long> Clock;
    public Action<KeyCode, int?, bool, long> Key;
    public Func<IEnumerable<Canvas>> Canvases;
    public Func<Canvas> SingleCanvas;
    public Action BeforeCapture;
    public Action AfterCapture;
    public Action<Action<string, RectInt, RectInt>> OverlayVideos;
  }

  private static Bound BindReplayMembers(MethodInfo[] methods, object instance, string typeName)
  {
    var bound = new Bound
    {
      End = BindAction(methods, instance, EndName, typeName),
      BeforeCapture = BindAction(methods, instance, BeforeCaptureName, typeName),
      AfterCapture = BindAction(methods, instance, AfterCaptureName, typeName),
    };

    MethodInfo clock = Single(methods, ClockName, typeName);
    if (clock != null)
      bound.Clock =
        (Action<long>)Delegate.CreateDelegate(typeof(Action<long>), instance, clock, false)
        ?? throw new RunStateModelException(
          typeName + "." + ClockName + " must be void " + ClockName + "(long ticks)."
        );

    MethodInfo key = Single(methods, KeyName, typeName);
    if (key != null)
      bound.Key =
        (Action<KeyCode, int?, bool, long>)
          Delegate.CreateDelegate(typeof(Action<KeyCode, int?, bool, long>), instance, key, false)
        ?? throw new RunStateModelException(
          typeName
            + "."
            + KeyName
            + " must be void "
            + KeyName
            + "(KeyCode key, int? nativeKey, bool down, long ticks)."
        );

    MethodInfo canvases = Single(methods, CanvasesName, typeName);
    if (canvases != null)
    {
      if (canvases.GetParameters().Length == 0 && typeof(IEnumerable<Canvas>).IsAssignableFrom(canvases.ReturnType))
        bound.Canvases =
          (Func<IEnumerable<Canvas>>)Delegate.CreateDelegate(typeof(Func<IEnumerable<Canvas>>), instance, canvases);
      else if (canvases.GetParameters().Length == 0 && typeof(Canvas).IsAssignableFrom(canvases.ReturnType))
        bound.SingleCanvas = (Func<Canvas>)Delegate.CreateDelegate(typeof(Func<Canvas>), instance, canvases);
      else
        throw new RunStateModelException(
          typeName + "." + CanvasesName + " must take no parameters and return a Canvas or IEnumerable<Canvas>."
        );
    }

    MethodInfo overlayVideos = Single(methods, OverlayVideosName, typeName);
    if (overlayVideos != null)
      bound.OverlayVideos =
        (Action<Action<string, RectInt, RectInt>>)
          Delegate.CreateDelegate(typeof(Action<Action<string, RectInt, RectInt>>), instance, overlayVideos, false)
        ?? throw new RunStateModelException(
          typeName
            + "."
            + OverlayVideosName
            + " must be void "
            + OverlayVideosName
            + "(Action<string, RectInt, RectInt> add)."
        );
    return bound;
  }

  private static Action BindAction(MethodInfo[] methods, object instance, string name, string typeName)
  {
    MethodInfo method = Single(methods, name, typeName);
    if (method == null)
      return null;
    if (method.GetParameters().Length != 0)
      throw new RunStateModelException(typeName + "." + name + " must take no parameters.");
    return (Action)Delegate.CreateDelegate(typeof(Action), instance, method, false)
      ?? throw new RunStateModelException(typeName + "." + name + " must be void " + name + "().");
  }

  public object InvokeCapture() => Invoke(_capture, null);

  public object ReadCaptureResult(Task task) => _captureResult.GetValue(task);

  public void InvokeBegin(bool render)
  {
    if (_begin != null)
      Invoke(_begin, _beginTakesRender ? new object[] { render } : null);
  }

  /// <summary>Hands the provider a value of its state type, or null, at <paramref name="ticks"/>.</summary>
  public void InvokeEvent(object value, long ticks)
  {
    if (_event != null)
      Invoke(_event, new[] { value, ticks });
  }

  public void InvokeEnd()
  {
    try
    {
      _end?.Invoke();
    }
    catch (Exception exception)
    {
      throw new ReplayProviderException(exception);
    }
  }

  private object Invoke(MethodInfo method, object[] arguments)
  {
    try
    {
      return method.Invoke(Instance, arguments);
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
      throw new ReplayProviderException(exception.InnerException);
    }
  }

  /// <summary>How a value saved with <paramref name="shapeId"/> is read. Cached per shape.</summary>
  public RunStateReadPlan PlanFor(string shapeId, string shapeJson)
  {
    if (Model == null)
      return RunStateReadPlan.Unreadable("this provider saves no state.");
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
internal sealed class ReplayProviderException : Exception
{
  public ReplayProviderException(Exception inner)
    : base(inner.Message, inner) { }
}
