using System;
using System.Collections.Generic;
using TUFReplay.RunState.Diagnostics;
using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Models;
using TUFReplay.RunState.Providers;

namespace TUFReplay.RunState.Playback;

/// <summary>
/// Hands saved states back while TUFReplay replays a run in the game or renders it through
/// OrbitRender. One session is active at a time; beginning another ends the previous one.
/// All calls happen on the Unity main thread.
/// </summary>
internal static class RunStateReplays
{
  private static volatile RunStateReplaySession _active;

  public static RunStateReplaySession Active => _active;

  public static RunStateReplaySession Begin(string runId, bool render, IReadOnlyList<StoredRunState> saved)
  {
    End(_active);
    var session = new RunStateReplaySession(runId, render);
    _active = session;

    var byId = new Dictionary<string, StoredRunState>(StringComparer.Ordinal);
    if (saved != null)
      foreach (StoredRunState state in saved)
        byId[state.StateId] = state;

    var begun = new List<RunStateProvider>();
    foreach (RunStateProvider provider in RunStateProviders.All)
    {
      if (!provider.FollowsReplays)
        continue;
      object recorded = byId.TryGetValue(provider.Id, out StoredRunState state) ? ReadOrNull(provider, state) : null;
      begun.Add(provider);
      try
      {
        provider.InvokeBegin(recorded, render);
      }
      catch (Exception exception)
      {
        RunStateLog.ProviderFailed(provider, RunStateProvider.BeginName, exception);
      }
    }
    session.Begun = begun.ToArray();
    return session;
  }

  /// <summary>Ends <paramref name="session"/> once. Providers unregistered since it began are not called.</summary>
  public static void End(RunStateReplaySession session)
  {
    if (session == null || session.Ended)
      return;
    session.Ended = true;
    if (_active == session)
      _active = null;
    foreach (RunStateProvider provider in session.Begun)
    {
      if (!RunStateProviders.IsCurrent(provider))
        continue;
      try
      {
        provider.InvokeEnd();
      }
      catch (Exception exception)
      {
        RunStateLog.ProviderFailed(provider, RunStateProvider.EndName, exception);
      }
    }
  }

  private static object ReadOrNull(RunStateProvider provider, StoredRunState state)
  {
    RunStateReadPlan plan = provider.PlanFor(state.ShapeId, state.ShapeJson);
    if (!plan.IsReadable)
    {
      ReportOnce(provider, state.ShapeId, "this run's saved value can't be read: " + plan.Problem);
      return null;
    }
    try
    {
      return provider.Read(plan, state.ValueJson);
    }
    catch (RunStateProviderException exception)
    {
      RunStateLog.ProviderFailed(provider, RunStateProvider.UpgradeName, exception);
      return null;
    }
    catch (RunStateValueException exception)
    {
      ReportOnce(provider, state.ShapeId, "this run's saved value can't be read: " + exception.Message);
      return null;
    }
  }

  private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

  private static void ReportOnce(RunStateProvider provider, string shapeId, string problem)
  {
    if (!Reported.Add(provider.Id + "/" + shapeId))
      return;
    RunStateLog.Write(
      "[RunState] " + provider.Id + ": " + problem + " " + RunStateProvider.BeginName + " gets null instead."
    );
  }

  internal static void ResetForTests()
  {
    _active = null;
    Reported.Clear();
  }
}

internal sealed class RunStateReplaySession
{
  public readonly string RunId;
  public readonly bool Render;
  internal RunStateProvider[] Begun = Array.Empty<RunStateProvider>();
  internal bool Ended;

  public RunStateReplaySession(string runId, bool render)
  {
    RunId = runId;
    Render = render;
  }

  public void End() => RunStateReplays.End(this);
}
