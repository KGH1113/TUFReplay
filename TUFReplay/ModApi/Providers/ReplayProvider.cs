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
/// <c>OnReplayState</c>'s state, which must be the same type. Each <c>OnReplayEvent</c>
/// overload takes one event type.
/// </summary>
internal sealed class ReplayProvider
{
  internal const string CaptureName = "CaptureRunState";
  internal const string UpgradeName = "UpgradeRunState";
  internal const string EventUpgradeName = "UpgradeRunEvent";
  internal const string BeginName = "OnReplayBegin";
  internal const string StateName = "OnReplayState";
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
    EventUpgradeName,
    BeginName,
    StateName,
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

  /// <summary>The state saved with each run, or null for a provider that saves none.</summary>
  public readonly RunValueTarget State;

  /// <summary>One per <c>OnReplayEvent</c> overload: the event types the provider adds and takes back.</summary>
  public readonly RunEventHandler[] Events;

  private readonly MethodInfo _capture;
  private readonly PropertyInfo _captureResult;
  private readonly MethodInfo _begin;
  private readonly bool _beginTakesRender;
  private readonly MethodInfo _state;
  private readonly Action _end;

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
    RunValueTarget state,
    RunEventHandler[] events,
    MethodInfo capture,
    MethodInfo begin,
    bool beginTakesRender,
    MethodInfo replayState,
    Bound bound
  )
  {
    Id = id;
    Instance = instance;
    State = state;
    Events = events;
    _capture = capture;
    _begin = begin;
    _beginTakesRender = beginTakesRender;
    _state = replayState;
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

  /// <summary>The state type, or null for a provider that saves none.</summary>
  public RunStateModel Model => State?.Model;

  public bool Captures => _capture != null;
  public bool CapturesAsync => _captureResult != null;

  /// <summary>Takes the run's start value back through <c>OnReplayState</c>.</summary>
  public bool TakesState => _state != null;

  /// <summary>Adds events during a run and takes them back through <c>OnReplayEvent</c>.</summary>
  public bool TakesEvents => Events.Length > 0;

  /// <summary>Has a state or events, so runs may hold values saved with older types.</summary>
  public bool SavesValues => State != null || Events.Length > 0;

  public bool FollowsReplays =>
    _begin != null
    || _state != null
    || Events.Length > 0
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
    if (_state != null)
      parts.Add("replay state");
    if (Events.Length > 0)
      parts.Add("events " + string.Join("/", Events.Select(handler => handler.Name)));
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

  /// <summary>The <c>OnReplayEvent</c> overload that takes exactly <paramref name="type"/>, or null.</summary>
  public RunEventHandler EventFor(Type type)
  {
    foreach (RunEventHandler handler in Events)
      if (handler.ClrType == type)
        return handler;
    return null;
  }

  /// <summary>
  /// The <c>OnReplayEvent</c> overload for events saved as <paramref name="savedName"/>: the same
  /// qualified name, or else the only one with the same type name, so a class moved to another
  /// namespace still reads its old runs. Null when none or several match.
  /// </summary>
  public RunEventHandler EventNamed(string savedName)
  {
    foreach (RunEventHandler handler in Events)
      if (handler.SavedName == savedName)
        return handler;
    string name = RunStateTypeNames.Short(savedName);
    RunEventHandler found = null;
    foreach (RunEventHandler handler in Events)
      if (handler.Name == name)
      {
        if (found != null)
          return null;
        found = handler;
      }
    return found;
  }

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
    MethodInfo replayState = Single(methods, StateName, typeName);
    Type captured = capture == null ? null : CapturedType(capture, typeName);
    bool beginTakesRender = begin != null && BeginTakesRender(begin, typeName);
    Type replayed = replayState == null ? null : ReplayedStateType(replayState, typeName);
    if (captured != null && replayed != null && captured != replayed)
      throw new RunStateModelException(
        typeName
          + "."
          + CaptureName
          + " returns "
          + RunStateTypeNames.Of(captured)
          + " but "
          + StateName
          + " takes "
          + RunStateTypeNames.Of(replayed)
          + ". Use the same state type in both."
      );

    Type stateType = captured ?? replayed;
    RunStateModel model = stateType == null ? null : RunStateModel.For(stateType);
    RunStateUpgrade[] upgrades = BindStateUpgrades(methods, model, typeName);
    RunValueTarget state = model == null ? null : new RunValueTarget(model, upgrades);
    RunEventHandler[] events = BindEvents(methods, typeName);
    if (events.Length > 0 && replayState == null)
      throw new RunStateModelException(MissingStateProblem(typeName, stateType, events));
    Bound bound = BindReplayMembers(methods, instance, typeName);
    return new ReplayProvider(id, instance, state, events, capture, begin, beginTakesRender, replayState, bound);
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
              + StateName
              + "("
              + RunStateTypeNames.Of(parameters[0].ParameterType)
              + " state, long ticks), which runs right after it."
            : ""
        )
    );
  }

  private static Type ReplayedStateType(MethodInfo replayState, string typeName)
  {
    ParameterInfo[] parameters = replayState.GetParameters();
    if (replayState.ReturnType != typeof(void) || parameters.Length != 2 || parameters[1].ParameterType != typeof(long))
      throw new RunStateModelException(
        typeName + "." + StateName + " must be void " + StateName + "(YourState state, long ticks)."
      );
    return parameters[0].ParameterType;
  }

  private static string MissingStateProblem(string typeName, Type stateType, RunEventHandler[] events)
  {
    // The shape ApiVersion 2 had before events got their own types: one OnReplayEvent that took
    // the start value as well as each value recorded later.
    if (stateType != null && events.Any(handler => handler.ClrType == stateType))
    {
      string state = RunStateTypeNames.Of(stateType);
      return typeName
        + "."
        + EventName
        + "("
        + state
        + " e, long ticks) no longer gets the run's start value. Take it in void "
        + StateName
        + "("
        + state
        + " state, long ticks), which runs right after "
        + BeginName
        + " and after each seek.";
    }
    return typeName
      + " takes events but has no "
      + StateName
      + ". Add void "
      + StateName
      + "("
      + (stateType == null ? "YourState" : RunStateTypeNames.Of(stateType))
      + " state, long ticks): a seek starts the replay over from the run's start value, then hands over the events up to there again.";
  }

  private static bool IsTaskOfState(Type type) =>
    type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);

  private static RunStateUpgrade[] BindStateUpgrades(MethodInfo[] methods, RunStateModel model, string typeName)
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
            + StateName
            + "(YourState state, long ticks), or remove it."
        );
      if (method.GetParameters().Length != 1 || method.ReturnType != model.ClrType)
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
      upgrades.Add(BindUpgrade(method, UpgradeName, model, upgrades, typeName, RunStateModel.For));
    }
    return upgrades.ToArray();
  }

  private static RunEventHandler[] BindEvents(MethodInfo[] methods, string typeName)
  {
    var methodsByType = new List<(MethodInfo Method, RunStateModel Model)>();
    foreach (MethodInfo method in methods.Where(method => method.Name == EventName))
    {
      ParameterInfo[] parameters = method.GetParameters();
      if (
        method.ReturnType != typeof(void)
        || method.IsGenericMethodDefinition
        || parameters.Length != 2
        || parameters[1].ParameterType != typeof(long)
      )
        throw new RunStateModelException(
          typeName
            + "."
            + EventName
            + " must be void "
            + EventName
            + "(YourEvent e, long ticks), one for each event type."
        );
      Type eventType = parameters[0].ParameterType;
      RunStateModel model;
      try
      {
        model = RunStateModel.ForEvent(eventType);
      }
      catch (RunStateModelException exception)
      {
        throw new RunStateModelException(
          typeName + "." + EventName + "(" + RunStateTypeNames.Of(eventType) + "): " + exception.Message
        );
      }
      string savedName = RunStateTypeNames.Qualified(eventType);
      if (methodsByType.Any(other => RunStateTypeNames.Qualified(other.Model.ClrType) == savedName))
        throw new RunStateModelException(
          typeName + "." + EventName + " takes two event types named " + savedName + ". Rename one."
        );
      methodsByType.Add((method, model));
    }

    var upgrades = methodsByType.ToDictionary(entry => entry.Model.ClrType, _ => new List<RunStateUpgrade>());
    foreach (MethodInfo method in methods.Where(method => method.Name == EventUpgradeName))
    {
      if (methodsByType.Count == 0)
        throw new RunStateModelException(
          typeName
            + "."
            + EventUpgradeName
            + " has no event type to upgrade to. Add "
            + EventName
            + "(YourEvent e, long ticks), or remove it."
        );
      if (
        method.GetParameters().Length != 1
        || !upgrades.TryGetValue(method.ReturnType, out List<RunStateUpgrade> list)
      )
      {
        string first = RunStateTypeNames.Of(methodsByType[0].Model.ClrType);
        throw new RunStateModelException(
          typeName
            + "."
            + EventUpgradeName
            + " must take one older event and return a type "
            + EventName
            + " takes ("
            + string.Join(", ", methodsByType.Select(entry => RunStateTypeNames.Of(entry.Model.ClrType)))
            + "), such as: "
            + first
            + " "
            + EventUpgradeName
            + "("
            + first
            + "V1 old)."
        );
      }
      RunStateModel target = methodsByType.First(entry => entry.Model.ClrType == method.ReturnType).Model;
      list.Add(BindUpgrade(method, EventUpgradeName, target, list, typeName, RunStateModel.ForEvent));
    }

    return methodsByType
      .Select(entry => new RunEventHandler(
        entry.Method,
        new RunValueTarget(entry.Model, upgrades[entry.Model.ClrType].ToArray())
      ))
      .ToArray();
  }

  /// <summary>Checks one upgrade method that turns an older type into <paramref name="target"/>.</summary>
  private static RunStateUpgrade BindUpgrade(
    MethodInfo method,
    string name,
    RunStateModel target,
    List<RunStateUpgrade> existing,
    string typeName,
    Func<Type, RunStateModel> map
  )
  {
    Type source = method.GetParameters()[0].ParameterType;
    RunStateModel sourceModel;
    try
    {
      sourceModel = map(source);
    }
    catch (RunStateModelException exception)
    {
      throw new RunStateModelException(
        typeName + "." + name + "(" + RunStateTypeNames.Of(source) + "): " + exception.Message
      );
    }
    if (sourceModel.ShapeId == target.ShapeId)
      throw new RunStateModelException(
        typeName
          + "."
          + name
          + " takes "
          + RunStateTypeNames.Of(source)
          + ", which has the same members as "
          + RunStateTypeNames.Of(target.ClrType)
          + ", so it can never be used. Give the old type the members it was saved with."
      );
    if (existing.Any(upgrade => upgrade.Source.ShapeId == sourceModel.ShapeId))
      throw new RunStateModelException(
        typeName + " has two " + name + " methods for types with the same members. Keep one."
      );
    return new RunStateUpgrade(sourceModel, method);
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

  /// <summary>Hands the provider its start value, or null, at <paramref name="ticks"/>.</summary>
  public void InvokeState(object state, long ticks)
  {
    if (_state != null)
      Invoke(_state, new[] { state, ticks });
  }

  /// <summary>Hands the provider one event through the overload for its type.</summary>
  public void InvokeEvent(RunEventHandler handler, object e, long ticks) => Invoke(handler.Method, new[] { e, ticks });

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

  /// <summary>Reads a saved value into the plan's type, upgrading it when the plan says so.</summary>
  public object Read(RunStateReadPlan plan, string valueJson)
  {
    if (plan.Upgrade == null)
      return RunStateValueReader.Read(plan.Into, valueJson);
    object old = RunStateValueReader.Read(plan.Upgrade.Source.Type, valueJson);
    return Invoke(plan.Upgrade.Method, new[] { old });
  }
}

/// <summary>
/// A type saved values are read back into: the provider's state type or one of its event types,
/// with the upgrade methods that return it. How each saved shape is read is worked out once.
/// </summary>
internal sealed class RunValueTarget
{
  public readonly RunStateModel Model;
  public readonly RunStateUpgrade[] Upgrades;
  private readonly ConcurrentDictionary<string, RunStateReadPlan> _plans = new ConcurrentDictionary<
    string,
    RunStateReadPlan
  >(StringComparer.Ordinal);

  public RunValueTarget(RunStateModel model, RunStateUpgrade[] upgrades)
  {
    Model = model;
    Upgrades = upgrades;
  }

  /// <summary>How a value saved with <paramref name="shapeId"/> is read. Cached per shape.</summary>
  public RunStateReadPlan PlanFor(string shapeId, string shapeJson) =>
    _plans.GetOrAdd(shapeId, _ => CreatePlan(shapeId, shapeJson));

  private RunStateReadPlan CreatePlan(string shapeId, string shapeJson)
  {
    if (shapeId == Model.ShapeId)
      return RunStateReadPlan.Direct(Model.Type, null);
    foreach (RunStateUpgrade upgrade in Upgrades)
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
      return RunStateReadPlan.Direct(Model.Type, direct);
    foreach (RunStateUpgrade upgrade in Upgrades)
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
}

/// <summary>One <c>OnReplayEvent</c> overload: the event type it takes, and how saved events of that type are read.</summary>
internal sealed class RunEventHandler
{
  public readonly MethodInfo Method;
  public readonly RunValueTarget Target;

  /// <summary>The name events of this type are saved under.</summary>
  public readonly string SavedName;

  public RunEventHandler(MethodInfo method, RunValueTarget target)
  {
    Method = method;
    Target = target;
    SavedName = RunStateTypeNames.Qualified(target.Model.ClrType);
  }

  public RunStateModel Model => Target.Model;
  public Type ClrType => Target.Model.ClrType;

  /// <summary>The event type as the mod's code writes it, for messages.</summary>
  public string Name => RunStateTypeNames.Of(ClrType);
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
  /// <summary>The type the saved value is read into directly, or null when an upgrade reads it.</summary>
  public RunStateType Into { get; private set; }

  public RunStateUpgrade Upgrade { get; private set; }

  /// <summary>Why the value can't be read, or null.</summary>
  public string Problem { get; private set; }

  /// <summary>The automatic conversion used, or null for an exact shape.</summary>
  public RunStateCompatibility Conversion { get; private set; }

  public bool IsReadable => Problem == null;

  public static RunStateReadPlan Direct(RunStateType into, RunStateCompatibility conversion) =>
    new RunStateReadPlan { Into = into, Conversion = conversion };

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
