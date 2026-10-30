using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TUFReplay.Shared.NativeInput;

/// <summary>
/// Native-input pauses requested by other mods, keyed by owner. Input stays paused while any
/// owner holds a request and for the same short settle as the UMM window after the last release.
/// </summary>
internal static class NativeInputPauseRequests
{
  private static readonly object Gate = new object();
  private static readonly HashSet<string> Owners = new HashSet<string>(StringComparer.Ordinal);

  // Mirrors Owners.Count != 0 so per-frame readers never take the lock.
  private static int _held;
  private static long _resumeAllowedAt;

  public static bool IsHeld => Volatile.Read(ref _held) != 0;

  public static bool IsBlocked => IsBlockedAt(Stopwatch.GetTimestamp());

  /// <returns>True when this request started the pause.</returns>
  internal static bool RequestAt(string owner, long timestamp)
  {
    lock (Gate)
    {
      if (!Owners.Add(owner) || Owners.Count != 1)
        return false;

      Interlocked.Exchange(ref _resumeAllowedAt, 0L);
      Volatile.Write(ref _held, 1);
      return true;
    }
  }

  /// <returns>True when this release ended the pause.</returns>
  internal static bool ReleaseAt(string owner, long timestamp)
  {
    lock (Gate)
    {
      if (!Owners.Remove(owner) || Owners.Count != 0)
        return false;

      Interlocked.Exchange(ref _resumeAllowedAt, timestamp + NativeInputPauseGate.ResumeDelayTicks);
      Volatile.Write(ref _held, 0);
      return true;
    }
  }

  internal static bool IsBlockedAt(long timestamp)
  {
    if (Volatile.Read(ref _held) != 0)
      return true;

    long resumeAllowedAt = Interlocked.Read(ref _resumeAllowedAt);
    return resumeAllowedAt != 0L && timestamp < resumeAllowedAt;
  }

  internal static void Reset()
  {
    lock (Gate)
    {
      Owners.Clear();
      Interlocked.Exchange(ref _resumeAllowedAt, 0L);
      Volatile.Write(ref _held, 0);
    }
  }
}
