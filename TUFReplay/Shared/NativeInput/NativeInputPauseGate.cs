using System;
using System.Diagnostics;

namespace TUFReplay.Shared.NativeInput;

/// <summary>
/// Whether TUFReplay may read or send native input now, across every pause source:
/// the UnityModManager window and requests from other mods.
/// </summary>
internal static class NativeInputPauseGate
{
  private const double ResumeDelaySeconds = 0.2d;

  // Lets the key or click that closed a window settle before input resumes.
  internal static readonly long ResumeDelayTicks = Math.Max(1L, (long)(Stopwatch.Frequency * ResumeDelaySeconds));

  public static bool IsBlocked => TryGetBlockReason(out _);

  /// <summary>True while a source holds the pause, not counting the settle after it ends.</summary>
  public static bool IsHeld => NativeInputUmmWindowInterlock.IsWindowOpen || NativeInputPauseRequests.IsHeld;

  public static bool TryGetBlockReason(out string reason)
  {
    if (NativeInputUmmWindowInterlock.IsBlocked)
    {
      reason = "umm_window_open_or_stabilizing";
      return true;
    }

    if (NativeInputPauseRequests.IsBlocked)
    {
      reason = "mod_pause_requested_or_stabilizing";
      return true;
    }

    reason = null;
    return false;
  }
}
