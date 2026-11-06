using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Playback;
using TUFReplay.RunState.Providers;

namespace TUFReplay;

/// <summary>
/// Lets other mods save their own state with each recorded run and get it back when that run
/// is replayed or rendered. A mod registers a provider object from its own assembly, and
/// TUFReplay calls its methods by name, so neither mod references the other:
/// <code>
///   TState       CaptureRunState()                       // or Task&lt;TState&gt;; when a run starts recording
///   void         OnReplayBegin(TState recorded, bool render) // or (TState recorded); null if nothing was saved
///   void         OnReplayEnd()
///   TState       UpgradeRunState(TOldState old)           // any number; reads runs saved with an older type
/// </code>
/// <c>TState</c> is the mod's own class. Its public fields and settable properties are saved
/// with their types, and every run keeps the shape it was saved with. See docs/run-state-api.md.
/// Consumers resolve this type by reflection instead of referencing TUFReplay.dll.
/// </summary>
public static class RunStateApi
{
  /// <summary>Changes only when an existing member or provider method changes its meaning.</summary>
  public const int ApiVersion = 1;

  /// <summary>The largest state saved for one run, in UTF-8 bytes of JSON.</summary>
  public const int MaxStateBytes = RunStateValueWriter.MaxBytes;

  /// <summary>
  /// True between <c>OnReplayBegin</c> and <c>OnReplayEnd</c>: while TUFReplay replays a run
  /// in the game or renders one through OrbitRender.
  /// </summary>
  public static bool IsReplaying => RunStateReplays.Active != null;

  /// <summary>True while the run being replayed is rendered through OrbitRender.</summary>
  public static bool IsRendering => RunStateReplays.Active?.Render == true;

  /// <summary>The id of the run being replayed or rendered, or null.</summary>
  public static string ReplayRunId => RunStateReplays.Active?.RunId;

  /// <summary>
  /// Registers <paramref name="provider"/> under <paramref name="id"/>, replacing a provider
  /// registered under the same id. Returns false if the id or provider is invalid; the reason
  /// is written to the mod log. Register from Unity's main thread, and retry when TUFReplay
  /// loads after your mod.
  /// </summary>
  /// <param name="id">A stable id saved with every run, such as <c>"MyMod.Attempts"</c>.</param>
  /// <param name="provider">An object with the provider methods above.</param>
  public static bool Register(string id, object provider) => RunStateProviders.Register(id, provider);

  /// <summary>Removes the provider registered under <paramref name="id"/>. Saved runs keep its states.</summary>
  public static void Unregister(string id) => RunStateProviders.Unregister(id);

  public static bool IsRegistered(string id) => RunStateProviders.IsRegistered(id);
}
