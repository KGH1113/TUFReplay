using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Playback;
using TUFReplay.ModApi.Providers;

namespace TUFReplay;

/// <summary>
/// How overlay, key-viewer and other mods take part in TUFReplay's recordings and replays.
/// A mod registers a provider object from its own assembly, and TUFReplay calls its public
/// methods by name, so neither mod references the other. Every method is optional:
/// <code>
///   // Recording: a state saved with each run, typed by the mod's own class.
///   TState CaptureRunState()                                   // or Task&lt;TState&gt;
///   TState UpgradeRunState(TOldState old)                      // any number; reads older runs
///
///   // Replay: the same calls whether a run is played in the game or rendered to video.
///   void   OnReplayBegin(TState recorded, bool render)         // or (TState), (bool) or ()
///   void   OnReplayClock(long ticks)                           // every frame
///   void   OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)
///   void   OnReplayEnd()
///
///   // Render only: what the video shows.
///   IEnumerable&lt;Canvas&gt; GetReplayCanvases()                 // or Canvas
///   void   OnBeforeFrameCapture()
///   void   OnAfterFrameCapture()
/// </code>
/// During a replay, take keys from <c>OnReplayKey</c> and time from <c>OnReplayClock</c> instead
/// of live input and real time. Every call is on Unity's main thread. See docs/replay-api.md.
/// Consumers resolve this type by reflection instead of referencing TUFReplay.dll.
/// </summary>
public static class ReplayApi
{
  /// <summary>Changes only when an existing member or provider method changes its meaning.</summary>
  public const int ApiVersion = 1;

  /// <summary>The largest state saved for one run, in UTF-8 bytes of JSON.</summary>
  public const int MaxStateBytes = RunStateValueWriter.MaxBytes;

  /// <summary>
  /// True between <c>OnReplayBegin</c> and <c>OnReplayEnd</c>: while TUFReplay plays a run in the
  /// game or renders one to video.
  /// </summary>
  public static bool IsReplaying => ReplaySessions.Active != null;

  /// <summary>True while the run being replayed is rendered to video.</summary>
  public static bool IsRendering => ReplaySessions.Active?.Render != null;

  /// <summary>The id of the run being replayed, or null.</summary>
  public static string ReplayRunId => ReplaySessions.Active?.RunId;

  /// <summary>
  /// The replay clock in 100-ns ticks (<c>TimeSpan.TicksPerSecond</c> per second), from 0 when
  /// the replay began. In a render it is the video's time; in the game, real time. It never goes
  /// back during a replay. 0 outside one.
  /// </summary>
  public static long ReplayTicks => ReplaySessions.Active?.Ticks ?? 0;

  /// <summary>The video's width in pixels while a replay is rendered, otherwise 0.</summary>
  public static int RenderWidth => ReplaySessions.Active?.Render?.Width ?? 0;

  /// <summary>The video's height in pixels while a replay is rendered, otherwise 0.</summary>
  public static int RenderHeight => ReplaySessions.Active?.Render?.Height ?? 0;

  /// <summary>The video's frames per second while a replay is rendered, otherwise 0.</summary>
  public static int RenderFps => ReplaySessions.Active?.Render?.VideoFps ?? 0;

  /// <summary>
  /// Registers <paramref name="provider"/> under <paramref name="id"/>, replacing a provider
  /// registered under the same id. Returns false if the id or provider is invalid; the reason
  /// is written to the mod log. Register from Unity's main thread, and retry when TUFReplay
  /// loads after your mod.
  /// </summary>
  /// <param name="id">A stable id, saved with every run when the provider has a state, such as <c>"MyMod"</c>.</param>
  /// <param name="provider">An object with any of the methods above.</param>
  public static bool Register(string id, object provider) => ReplayProviders.Register(id, provider);

  /// <summary>Removes the provider registered under <paramref name="id"/>. Saved runs keep its states.</summary>
  public static void Unregister(string id) => ReplayProviders.Unregister(id);

  public static bool IsRegistered(string id) => ReplayProviders.IsRegistered(id);
}
