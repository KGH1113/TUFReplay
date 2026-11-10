using System;
using System.Collections.Generic;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Providers;

namespace TUFReplay.ModApi.Playback;

/// <summary>
/// Hands providers the values they recorded during a run, at their times on the run's timeline,
/// through <c>OnReplayEvent</c>. A value is read from JSON only when it is handed over, so a
/// frame without one allocates nothing. After a seek, each provider gets the value in effect at
/// the new time if it differs from the last one it got.
/// </summary>
internal sealed class ReplayRunEvents
{
  /// <summary>The provider's start value, as opposed to one of the recorded values.</summary>
  private const int StartValue = -1;

  private readonly ReplaySession _session;
  private readonly Track[] _tracks;
  private readonly StoredRunEvent[] _events;
  private readonly int[] _trackOf;
  private readonly int[] _inEffect;
  private int _next;

  private ReplayRunEvents(ReplaySession session, Track[] tracks, StoredRunEvent[] events, int[] trackOf)
  {
    _session = session;
    _tracks = tracks;
    _events = events;
    _trackOf = trackOf;
    _inEffect = new int[tracks.Length];
  }

  /// <summary>
  /// The recorded values of <paramref name="begun"/> providers that take events, in time order.
  /// <paramref name="startValues"/> holds each provider's start value by id.
  /// </summary>
  internal static ReplayRunEvents For(
    ReplaySession session,
    ReplayProvider[] begun,
    IReadOnlyDictionary<string, StoredRunState> startValues,
    IReadOnlyList<StoredRunEvent> events
  )
  {
    var tracks = new List<Track>();
    var trackById = new Dictionary<string, int>(StringComparer.Ordinal);
    for (int index = 0; index < begun.Length; index++)
    {
      ReplayProvider provider = begun[index];
      if (!provider.TakesEvents || provider.Model == null)
        continue;
      startValues.TryGetValue(provider.Id, out StoredRunState start);
      trackById[provider.Id] = tracks.Count;
      tracks.Add(new Track(provider, index, start));
    }

    var kept = new List<StoredRunEvent>();
    var trackOf = new List<int>();
    if (events != null)
      foreach (StoredRunEvent recorded in events)
        if (trackById.TryGetValue(recorded.StateId, out int track))
        {
          kept.Add(recorded);
          trackOf.Add(track);
        }
    return new ReplayRunEvents(session, tracks.ToArray(), kept.ToArray(), trackOf.ToArray());
  }

  /// <summary>Recorded values still to hand over, for tests.</summary>
  internal int Remaining => _events.Length - _next;

  /// <summary>
  /// Hands over every value recorded up to <paramref name="replayTimeUs"/>. A late value arrives
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
  /// Hands over every value recorded up to <paramref name="replayTimeUs"/>, each at the session
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
  /// Moves to <paramref name="replayTimeUs"/> without handing over the values in between. A
  /// provider whose value in effect there differs from the last one it got gets it now.
  /// </summary>
  public void SeekTo(long replayTimeUs, long nowTicks)
  {
    for (int track = 0; track < _inEffect.Length; track++)
      _inEffect[track] = StartValue;
    int next = 0;
    while (next < _events.Length && _events[next].TimeUs <= replayTimeUs)
    {
      _inEffect[_trackOf[next]] = next;
      next++;
    }
    _next = next;
    for (int track = 0; track < _tracks.Length; track++)
      if (_inEffect[track] != _tracks[track].Given)
        Give(track, _inEffect[track], nowTicks);
  }

  /// <summary>Hands every provider its start value, right after its <c>OnReplayBegin</c>.</summary>
  internal void GiveStartValue(ReplayProvider provider)
  {
    for (int track = 0; track < _tracks.Length; track++)
      if (_tracks[track].Provider == provider)
      {
        Give(track, StartValue, 0);
        return;
      }
  }

  private void Hand(int index, long ticks) => Give(_trackOf[index], index, ticks);

  private void Give(int track, int index, long ticks)
  {
    Track target = _tracks[track];
    target.Given = index;
    if (!ReplayProviders.IsCurrent(target.Provider))
      return;

    object value;
    if (index == StartValue)
    {
      // A run without a readable start value still tells the provider so, as null.
      value =
        target.Start == null
          ? null
          : ReplaySessions.ReadOrNull(
            target.Provider,
            target.Start.ShapeId,
            target.Start.ShapeJson,
            target.Start.ValueJson,
            recorded: false
          );
    }
    else
    {
      StoredRunEvent recorded = _events[index];
      value = ReplaySessions.ReadOrNull(
        target.Provider,
        recorded.ShapeId,
        recorded.ShapeJson,
        recorded.ValueJson,
        recorded: true
      );
      if (value == null)
        return;
    }

    try
    {
      target.Provider.InvokeEvent(value, ticks);
    }
    catch (Exception exception)
    {
      ReplaySessions.ReportFrameFailure(_session, target.BegunIndex, ReplayProvider.EventName, exception);
    }
  }

  private sealed class Track
  {
    public readonly ReplayProvider Provider;
    public readonly int BegunIndex;
    public readonly StoredRunState Start;

    /// <summary>The value the provider got last: <see cref="StartValue"/> or an index into the events.</summary>
    public int Given = StartValue;

    public Track(ReplayProvider provider, int begunIndex, StoredRunState start)
    {
      Provider = provider;
      BegunIndex = begunIndex;
      Start = start;
    }
  }
}
