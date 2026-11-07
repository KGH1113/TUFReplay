using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using TUFReplay.ModApi.Diagnostics;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;
using UnityEngine;

namespace TUFReplay.ModApi.Playback;

/// <summary>
/// The replay mods see through <see cref="ReplayApi"/>, whether TUFReplay plays it in the game or
/// renders it to video. One session runs at a time; beginning another ends the previous one.
/// Every call happens on the Unity main thread, and per-frame calls allocate nothing.
/// </summary>
internal static class ReplaySessions
{
  private const int MaxReportedFailures = 3;

  private static volatile ReplaySession _active;

  public static ReplaySession Active => _active;

  /// <summary>The session clock now: video time in a render, real time in the game. 0 without a session.</summary>
  public static long NowTicks
  {
    get
    {
      ReplaySession session = _active;
      if (session == null)
        return 0;
      return session.Render != null ? session.Ticks : session.ElapsedTicks;
    }
  }

  /// <summary>
  /// Begins a session and calls each provider's <c>OnReplayBegin</c> with the state saved for
  /// it. <paramref name="render"/> is null for a replay played in the game.
  /// </summary>
  public static ReplaySession Begin(string runId, ReplayRender render, IReadOnlyList<StoredRunState> saved)
  {
    End(_active);
    var session = new ReplaySession(runId, render);
    _active = session;

    var byId = new Dictionary<string, StoredRunState>(StringComparer.Ordinal);
    if (saved != null)
      foreach (StoredRunState state in saved)
        byId[state.StateId] = state;

    var begun = new List<ReplayProvider>();
    foreach (ReplayProvider provider in ReplayProviders.All)
    {
      if (!provider.FollowsReplays)
        continue;
      object recorded =
        provider.Model != null && byId.TryGetValue(provider.Id, out StoredRunState state)
          ? ReadOrNull(provider, state)
          : null;
      begun.Add(provider);
      try
      {
        provider.InvokeBegin(recorded, render != null);
      }
      catch (Exception exception)
      {
        ModApiLog.ProviderFailed(provider, ReplayProvider.BeginName, exception);
      }
    }
    session.Begun = begun.ToArray();
    session.Failures = new int[session.Begun.Length];
    return session;
  }

  /// <summary>
  /// Advances an in-game session's clock from real time. The mod calls this every frame; it does
  /// nothing while no in-game session runs.
  /// </summary>
  public static void TickInGame()
  {
    ReplaySession session = _active;
    if (session == null || session.Render != null || session.Ended)
      return;
    Clock(session, session.ElapsedTicks);
  }

  /// <summary>The render's actual video size and rate, once the renderer has settled them.</summary>
  public static void UpdateRender(ReplayRender render)
  {
    ReplaySession session = _active;
    if (session != null && session.Render != null && render != null)
      session.Render = render;
  }

  /// <summary>Sets the session clock, in 100-ns ticks from the session's start, and tells providers.</summary>
  public static void Clock(long ticks)
  {
    ReplaySession session = _active;
    if (session != null && !session.Ended)
      Clock(session, ticks);
  }

  private static void Clock(ReplaySession session, long ticks)
  {
    if (ticks < session.Ticks)
      ticks = session.Ticks;
    session.Ticks = ticks;
    ReplayProvider[] providers = session.Begun;
    for (int index = 0; index < providers.Length; index++)
    {
      ReplayProvider provider = providers[index];
      if (provider.Clock == null || !ReplayProviders.IsCurrent(provider))
        continue;
      try
      {
        provider.Clock(ticks);
      }
      catch (Exception exception)
      {
        ReportFrameFailure(session, index, ReplayProvider.ClockName, exception);
      }
    }
  }

  /// <summary>A recorded key, at <paramref name="ticks"/> on the session clock.</summary>
  public static void Key(KeyCode key, int? nativeKey, bool down, long ticks)
  {
    ReplaySession session = _active;
    if (session == null || session.Ended)
      return;
    ReplayProvider[] providers = session.Begun;
    for (int index = 0; index < providers.Length; index++)
    {
      ReplayProvider provider = providers[index];
      if (provider.Key == null || !ReplayProviders.IsCurrent(provider))
        continue;
      try
      {
        provider.Key(key, nativeKey, down, ticks);
      }
      catch (Exception exception)
      {
        ReportFrameFailure(session, index, ReplayProvider.KeyName, exception);
      }
    }
  }

  /// <summary>Adds the canvases providers want in the video to <paramref name="into"/>. Renders only.</summary>
  public static void CollectCanvases(List<Canvas> into)
  {
    ReplaySession session = _active;
    if (session == null || session.Ended || session.Render == null)
      return;
    ReplayProvider[] providers = session.Begun;
    for (int index = 0; index < providers.Length; index++)
    {
      ReplayProvider provider = providers[index];
      if (!provider.ProvidesCanvases || !ReplayProviders.IsCurrent(provider))
        continue;
      try
      {
        if (provider.SingleCanvas != null)
        {
          Canvas canvas = provider.SingleCanvas();
          if (canvas != null)
            into.Add(canvas);
          continue;
        }
        IEnumerable<Canvas> canvases = provider.Canvases();
        if (canvases == null)
          continue;
        foreach (Canvas canvas in canvases)
          if (canvas != null)
            into.Add(canvas);
      }
      catch (Exception exception)
      {
        ReportFrameFailure(session, index, ReplayProvider.CanvasesName, exception);
      }
    }
  }

  /// <summary>Just before a render captures a frame.</summary>
  public static void BeforeCapture() => Capture(before: true);

  /// <summary>Just after a render captured a frame.</summary>
  public static void AfterCapture() => Capture(before: false);

  private static void Capture(bool before)
  {
    ReplaySession session = _active;
    if (session == null || session.Ended || session.Render == null)
      return;
    ReplayProvider[] providers = session.Begun;
    for (int index = 0; index < providers.Length; index++)
    {
      ReplayProvider provider = providers[index];
      Action hook = before ? provider.BeforeCapture : provider.AfterCapture;
      if (hook == null || !ReplayProviders.IsCurrent(provider))
        continue;
      try
      {
        hook();
      }
      catch (Exception exception)
      {
        ReportFrameFailure(
          session,
          index,
          before ? ReplayProvider.BeforeCaptureName : ReplayProvider.AfterCaptureName,
          exception
        );
      }
    }
  }

  /// <summary>Ends <paramref name="session"/> once. Providers unregistered since it began are not called.</summary>
  public static void End(ReplaySession session)
  {
    if (session == null || session.Ended)
      return;
    session.Ended = true;
    if (_active == session)
      _active = null;
    foreach (ReplayProvider provider in session.Begun)
    {
      if (!ReplayProviders.IsCurrent(provider))
        continue;
      try
      {
        provider.InvokeEnd();
      }
      catch (Exception exception)
      {
        ModApiLog.ProviderFailed(provider, ReplayProvider.EndName, exception);
      }
    }
  }

  private static void ReportFrameFailure(ReplaySession session, int index, string member, Exception exception)
  {
    if (++session.Failures[index] > MaxReportedFailures)
      return;
    ModApiLog.ProviderFailed(session.Begun[index], member, exception);
  }

  private static object ReadOrNull(ReplayProvider provider, StoredRunState state)
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
    catch (ReplayProviderException exception)
    {
      ModApiLog.ProviderFailed(provider, ReplayProvider.UpgradeName, exception);
      return null;
    }
    catch (RunStateValueException exception)
    {
      ReportOnce(provider, state.ShapeId, "this run's saved value can't be read: " + exception.Message);
      return null;
    }
  }

  private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

  private static void ReportOnce(ReplayProvider provider, string shapeId, string problem)
  {
    if (!Reported.Add(provider.Id + "/" + shapeId))
      return;
    ModApiLog.Write(
      "[ReplayApi] " + provider.Id + ": " + problem + " " + ReplayProvider.BeginName + " gets null instead."
    );
  }

  internal static void ResetForTests()
  {
    _active = null;
    Reported.Clear();
  }
}

/// <summary>The video a render session draws into.</summary>
internal sealed class ReplayRender
{
  public readonly int Width;
  public readonly int Height;
  public readonly int VideoFps;

  public ReplayRender(int width, int height, int videoFps)
  {
    Width = width;
    Height = height;
    VideoFps = videoFps;
  }
}

internal sealed class ReplaySession
{
  public readonly string RunId;

  /// <summary>The video being rendered, or null for a replay played in the game.</summary>
  public ReplayRender Render { get; internal set; }

  private readonly long _startTimestamp = Stopwatch.GetTimestamp();
  private long _ticks;

  internal ReplayProvider[] Begun = Array.Empty<ReplayProvider>();
  internal int[] Failures = Array.Empty<int>();
  internal bool Ended;

  public ReplaySession(string runId, ReplayRender render)
  {
    RunId = runId;
    Render = render;
  }

  /// <summary>The session clock in 100-ns ticks. Read from any thread.</summary>
  public long Ticks
  {
    get => Interlocked.Read(ref _ticks);
    internal set => Interlocked.Exchange(ref _ticks, value);
  }

  /// <summary>Real time since the session began, in 100-ns ticks.</summary>
  internal long ElapsedTicks
  {
    get
    {
      long elapsed = Stopwatch.GetTimestamp() - _startTimestamp;
      return (long)(elapsed * (10_000_000.0 / Stopwatch.Frequency));
    }
  }

  public void End() => ReplaySessions.End(this);
}
