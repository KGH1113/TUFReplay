using System;
using System.Collections.Generic;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;

namespace TUFReplay.ModApi.Playback;

/// <summary>
/// Plays back what providers saved with a run. Right after its <c>OnReplayBegin</c>, a provider
/// gets the run's start value through <c>OnReplayState</c>; then each event it added reaches the
/// <c>OnReplayEvent</c> overload for its type, at its time on the run's timeline. A seek starts
/// each provider over: the start value again, then every event up to the new time, all at the
/// seek's time. An event is read from JSON only when it is handed over, so a frame without one
/// allocates nothing.
/// </summary>
internal sealed class ReplayRunEvents
{
  private readonly ReplaySession _session;
  private readonly Follower[] _followers;
  private readonly StoredRunEvent[] _events;
  private readonly int[] _followerOf;
  private readonly RunEventHandler[] _handlerOf;
  private int _next;

  private ReplayRunEvents(
    ReplaySession session,
    Follower[] followers,
    StoredRunEvent[] events,
    int[] followerOf,
    RunEventHandler[] handlerOf
  )
  {
    _session = session;
    _followers = followers;
    _events = events;
    _followerOf = followerOf;
    _handlerOf = handlerOf;
  }

  /// <summary>
  /// The start values and events of <paramref name="begun"/> providers that take them back.
  /// <paramref name="startValues"/> holds each provider's start value by id; <paramref name="events"/>
  /// are in time order. Events no <c>OnReplayEvent</c> overload takes are left out and reported once.
  /// </summary>
  internal static ReplayRunEvents For(
    ReplaySession session,
    ReplayProvider[] begun,
    IReadOnlyDictionary<string, StoredRunState> startValues,
    IReadOnlyList<StoredRunEvent> events
  )
  {
    var followers = new List<Follower>();
    var followerById = new Dictionary<string, int>(StringComparer.Ordinal);
    for (int index = 0; index < begun.Length; index++)
    {
      ReplayProvider provider = begun[index];
      if (!provider.TakesState && !provider.TakesEvents)
        continue;
      startValues.TryGetValue(provider.Id, out StoredRunState start);
      followerById[provider.Id] = followers.Count;
      followers.Add(new Follower(provider, index, start));
    }

    var kept = new List<StoredRunEvent>();
    var followerOf = new List<int>();
    var handlerOf = new List<RunEventHandler>();
    if (events != null)
      foreach (StoredRunEvent saved in events)
      {
        if (!followerById.TryGetValue(saved.StateId, out int follower))
          continue;
        ReplayProvider provider = followers[follower].Provider;
        RunEventHandler handler = provider.EventNamed(saved.EventType);
        if (handler == null)
        {
          ReplaySessions.ReportMissingEventHandler(provider, saved.EventType);
          continue;
        }
        kept.Add(saved);
        followerOf.Add(follower);
        handlerOf.Add(handler);
      }
    return new ReplayRunEvents(session, followers.ToArray(), kept.ToArray(), followerOf.ToArray(), handlerOf.ToArray());
  }

  /// <summary>Events still to hand over, for tests.</summary>
  internal int Remaining => _events.Length - _next;

  /// <summary>
  /// Hands over every event added up to <paramref name="replayTimeUs"/>. A late event arrives
  /// with the session time it should have had, measured back from <paramref name="nowTicks"/>.
  /// </summary>
  public void AdvanceTo(long replayTimeUs, long nowTicks)
  {
    while (_next < _events.Length && _events[_next].TimeUs <= replayTimeUs)
    {
      long ticks = nowTicks - (replayTimeUs - _events[_next].TimeUs) * 10;
      Hand(_next, ticks < 0 ? 0 : ticks);
      _next++;
    }
  }

  /// <summary>
  /// Hands over every event added up to <paramref name="replayTimeUs"/>, each at the session
  /// time <paramref name="ticksAt"/> gives for its replay time. Renders map replay time to video time.
  /// </summary>
  public void AdvanceTo(long replayTimeUs, Func<long, long> ticksAt)
  {
    while (_next < _events.Length && _events[_next].TimeUs <= replayTimeUs)
    {
      Hand(_next, ticksAt(_events[_next].TimeUs));
      _next++;
    }
  }

  /// <summary>
  /// Moves to <paramref name="replayTimeUs"/>. Each provider starts over: its start value, then
  /// every one of its events up to there, in order, all at <paramref name="nowTicks"/>.
  /// </summary>
  public void SeekTo(long replayTimeUs, long nowTicks)
  {
    int next = 0;
    while (next < _events.Length && _events[next].TimeUs <= replayTimeUs)
      next++;
    _next = next;
    for (int follower = 0; follower < _followers.Length; follower++)
    {
      GiveStartValue(follower, nowTicks);
      for (int index = 0; index < next; index++)
        if (_followerOf[index] == follower)
          Hand(index, nowTicks);
    }
  }

  /// <summary>Hands <paramref name="provider"/> its start value, right after its <c>OnReplayBegin</c>.</summary>
  internal void GiveStartValue(ReplayProvider provider)
  {
    for (int follower = 0; follower < _followers.Length; follower++)
      if (_followers[follower].Provider == provider)
      {
        GiveStartValue(follower, 0);
        return;
      }
  }

  private void GiveStartValue(int follower, long ticks)
  {
    Follower target = _followers[follower];
    ReplayProvider provider = target.Provider;
    if (!provider.TakesState || !ReplayProviders.IsCurrent(provider))
      return;

    // A run without a readable start value still tells the provider so, as null.
    StoredRunState start = target.Start;
    object state =
      start == null
        ? null
        : ReplaySessions.ReadOrNull(provider, provider.State, null, start.ShapeId, start.ShapeJson, start.ValueJson);
    try
    {
      provider.InvokeState(state, ticks);
    }
    catch (Exception exception)
    {
      ReplaySessions.ReportFrameFailure(_session, target.BegunIndex, ReplayProvider.StateName, exception);
    }
  }

  private void Hand(int index, long ticks)
  {
    Follower target = _followers[_followerOf[index]];
    ReplayProvider provider = target.Provider;
    if (!ReplayProviders.IsCurrent(provider))
      return;

    StoredRunEvent saved = _events[index];
    RunEventHandler handler = _handlerOf[index];
    object e = ReplaySessions.ReadOrNull(
      provider,
      handler.Target,
      handler.Name,
      saved.ShapeId,
      saved.ShapeJson,
      saved.ValueJson
    );
    if (e == null)
      return;
    try
    {
      provider.InvokeEvent(handler, e, ticks);
    }
    catch (Exception exception)
    {
      ReplaySessions.ReportFrameFailure(_session, target.BegunIndex, ReplayProvider.EventName, exception);
    }
  }

  private sealed class Follower
  {
    public readonly ReplayProvider Provider;
    public readonly int BegunIndex;
    public readonly StoredRunState Start;

    public Follower(ReplayProvider provider, int begunIndex, StoredRunState start)
    {
      Provider = provider;
      BegunIndex = begunIndex;
      Start = start;
    }
  }
}
