using TUFReplay.Replay.Preparation;
using TUFReplay.Replay.Sessions;

namespace TUFReplay;

/// <summary>
/// Stable, dependency-free runtime state for optional integrations with other mods.
/// Consumers may resolve this type by reflection instead of referencing TUFReplay.dll.
/// </summary>
public static class ReplayRuntime
{
  public const int ApiVersion = 1;

  /// <summary>
  /// True while TUFReplay owns a replay operation, including preparation, level loading,
  /// playback, and the return to the editor.
  /// </summary>
  public static bool IsPlaybackActive => ReplayPlaybackCoordinator.IsBusy;
}
