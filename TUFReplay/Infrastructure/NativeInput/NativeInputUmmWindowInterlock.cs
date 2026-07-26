using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using UnityModManagerNet;

namespace TUFReplay.Infrastructure.NativeInput;

internal static class NativeInputUmmWindowInterlock
{
  private const double ResumeDelaySeconds = 0.2d;
  private static readonly long ResumeDelayTicks = Math.Max(
    1L,
    (long)(Stopwatch.Frequency * ResumeDelaySeconds)
  );
  private static int _windowOpen;
  private static long _resumeAllowedAt;
  private static int _observedManagerWindowOpen = -1;

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
