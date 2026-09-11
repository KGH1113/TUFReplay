using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityModManagerNet;

namespace TUFReplay.Shared.NativeInput;

internal static class NativeInputUmmWindowInterlock
{
  private const double ResumeDelaySeconds = 0.2d;
  private const double FallbackPollIntervalSeconds = 1d;
  private static readonly long ResumeDelayTicks = Math.Max(1L, (long)(Stopwatch.Frequency * ResumeDelaySeconds));
  internal static readonly long FallbackPollIntervalTicks = Math.Max(
    1L,
    (long)(Stopwatch.Frequency * FallbackPollIntervalSeconds)
  );
  private static int _windowOpen;
  private static long _resumeAllowedAt;
  private static int _observedManagerWindowOpen = -1;
  private static int _fallbackPollingEnabled = 1;
  private static long _nextFallbackPollAt;

  public static bool IsBlocked
  {
    get => IsBlockedAt(Stopwatch.GetTimestamp());
  }

  public static void NotifyWindowOpen(bool open)
  {
    SetWindowOpenAt(open, Stopwatch.GetTimestamp());
    Volatile.Write(ref _observedManagerWindowOpen, open ? 1 : 0);
  }

  public static void SynchronizeWithManagerWindow()
  {
    if (Volatile.Read(ref _fallbackPollingEnabled) == 0)
      return;
    if (!ShouldPollManagerWindowAt(Stopwatch.GetTimestamp()))
      return;
    SynchronizeWithManagerWindowNow();
  }

  internal static void ConfigureManagerWindowPatch(bool available, bool synchronize = true)
  {
    Volatile.Write(ref _fallbackPollingEnabled, available ? 0 : 1);
    Interlocked.Exchange(ref _nextFallbackPollAt, 0L);
    if (synchronize)
      SynchronizeWithManagerWindowNow();
  }

  internal static bool ShouldPollManagerWindowAt(long timestamp)
  {
    if (Volatile.Read(ref _fallbackPollingEnabled) == 0)
      return false;

    while (true)
    {
      long next = Interlocked.Read(ref _nextFallbackPollAt);
      if (next != 0L && timestamp < next)
        return false;
      if (Interlocked.CompareExchange(ref _nextFallbackPollAt, timestamp + FallbackPollIntervalTicks, next) == next)
        return true;
    }
  }

  private static void SynchronizeWithManagerWindowNow()
  {
    if (!TryReadManagerWindowOpen(out bool open))
      return;

    int next = open ? 1 : 0;
    int previous = Interlocked.Exchange(ref _observedManagerWindowOpen, next);
    if (previous == next || (previous < 0 && !open))
      return;

    SetWindowOpenAt(open, Stopwatch.GetTimestamp());
  }

  public static void Reset()
  {
    Volatile.Write(ref _windowOpen, 0);
    Interlocked.Exchange(ref _resumeAllowedAt, 0L);
    Volatile.Write(ref _observedManagerWindowOpen, -1);
    Volatile.Write(ref _fallbackPollingEnabled, 1);
    Interlocked.Exchange(ref _nextFallbackPollAt, 0L);
  }

  internal static bool IsBlockedAt(long timestamp)
  {
    if (Volatile.Read(ref _windowOpen) != 0)
      return true;

    long resumeAllowedAt = Interlocked.Read(ref _resumeAllowedAt);
    return resumeAllowedAt != 0L && timestamp < resumeAllowedAt;
  }

  internal static void SetWindowOpenAt(bool open, long timestamp)
  {
    if (open)
    {
      Volatile.Write(ref _windowOpen, 1);
      Interlocked.Exchange(ref _resumeAllowedAt, 0L);
      return;
    }

    Interlocked.Exchange(ref _resumeAllowedAt, timestamp + ResumeDelayTicks);
    Volatile.Write(ref _windowOpen, 0);
  }

  private static bool TryReadManagerWindowOpen(out bool open)
  {
    open = false;
    try
    {
      object instance = ManagerUiReflection.InstanceProperty?.GetValue(null, null);
      if (instance == null || ManagerUiReflection.OpenedProperty == null)
        return false;

      open = ManagerUiReflection.OpenedProperty.GetValue(instance, null) is true;
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static class ManagerUiReflection
  {
    private static readonly Type UiType = typeof(UnityModManager).GetNestedType(
      "UI",
      BindingFlags.Public | BindingFlags.NonPublic
    );

    public static readonly PropertyInfo InstanceProperty = UiType?.GetProperty(
      "Instance",
      BindingFlags.Public | BindingFlags.Static
    );

    public static readonly PropertyInfo OpenedProperty = UiType?.GetProperty(
      "Opened",
      BindingFlags.Public | BindingFlags.Instance
    );
  }
}
