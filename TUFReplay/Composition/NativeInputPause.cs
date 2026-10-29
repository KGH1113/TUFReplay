using System;
using System.Diagnostics;
using TUFReplay.Composition;
using TUFReplay.Shared.NativeInput;
using TUFReplay.Shared.Unity;

namespace TUFReplay;

/// <summary>
/// Lets other mods pause TUFReplay's native input while their own UI owns the keyboard.
/// While paused, recording ignores native keyboard and mouse-button input and replay sends none,
/// exactly as while the UnityModManager window is open.
/// Consumers may resolve this type by reflection instead of referencing TUFReplay.dll.
/// </summary>
public static class NativeInputPause
{
  /// <summary>
  /// Changes only when an existing member changes its signature or meaning; new members keep it.
  /// </summary>
  public const int ApiVersion = 1;

  private static readonly Action SuspendIfHeld = () =>
  {
    if (NativeInputPauseRequests.IsHeld)
      NativeInputSuspension.Suspend("mod_pause");
  };

  private static readonly Action ResumeIfReleased = NativeInputSuspension.Resume;

  /// <summary>
  /// True while the UnityModManager window or any mod's request pauses native input,
  /// including the short settle after the last one ends.
  /// </summary>
  public static bool IsPaused => NativeInputPauseGate.IsBlocked;

  /// <summary>
  /// Pauses native input on behalf of <paramref name="owner"/> until the same owner calls
  /// <see cref="Release"/>. Requests are keyed by owner, so repeating one is harmless and a single
  /// release ends it. Callable from any thread; recording and replay react on the Unity main thread.
  /// </summary>
  /// <param name="owner">A stable id unique to the caller, such as <c>"MyMod.Settings"</c>.</param>
  public static void Request(string owner)
  {
    if (NativeInputPauseRequests.RequestAt(RequireOwner(owner), Stopwatch.GetTimestamp()))
      UnityMainThread.Post(SuspendIfHeld);
  }

  /// <summary>
  /// Ends <paramref name="owner"/>'s request. Native input resumes about 0.2 s after the last
  /// request ends and the UnityModManager window is closed. Releasing an owner that holds no
  /// request does nothing. Release when your UI closes and when your mod is disabled.
  /// </summary>
  public static void Release(string owner)
  {
    if (NativeInputPauseRequests.ReleaseAt(RequireOwner(owner), Stopwatch.GetTimestamp()))
      UnityMainThread.Post(ResumeIfReleased);
  }

  private static string RequireOwner(string owner)
  {
    if (string.IsNullOrWhiteSpace(owner))
      throw new ArgumentException("Pass a stable, non-empty owner id such as \"MyMod.Settings\".", nameof(owner));
    return owner;
  }
}
