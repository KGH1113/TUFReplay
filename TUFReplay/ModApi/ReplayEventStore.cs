using TUFReplay.ModApi.Recording;

namespace TUFReplay;

/// <summary>
/// One mod's events in TUFReplay's recordings, from <see cref="ReplayApi.GetEventStore"/>. While
/// a run records, <see cref="AddEvent"/> saves an event with the run at that moment. When the run
/// is replayed or rendered, the provider gets it back through the <c>OnReplayEvent</c> overload
/// for its type, at that time. Consumers bind <see cref="AddEvent"/> by reflection.
/// </summary>
public sealed class ReplayEventStore
{
  internal ReplayEventStore(string id) => Id = id;

  /// <summary>The provider id the events belong to.</summary>
  public string Id { get; }

  /// <summary>
  /// Adds <paramref name="e"/> to the run recording now, at this moment on its timeline: the
  /// same clock as its keys and hits. Its type must be exactly one the provider's
  /// <c>OnReplayEvent</c> overloads take, and it is written to JSON now, so changing the object
  /// later changes nothing. Every event is kept, several at one moment too. Call on Unity's main
  /// thread. Returns false when no run is recording, which is normal, and when the event can't be
  /// saved; that reason is written to the mod log.
  /// </summary>
  public bool AddEvent(object e) => RunStateCapture.AddEvent(Id, e);
}
