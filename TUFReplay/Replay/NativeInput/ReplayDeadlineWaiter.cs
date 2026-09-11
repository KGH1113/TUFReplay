using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace TUFReplay.Replay.NativeInput;

internal enum ReplayDeadlineWaitResult
{
  Deadline,
  Woken,
}

internal interface IReplayDeadlineWaiter : IDisposable
{
  string Name { get; }
  string FallbackReason { get; }
  ReplayDeadlineWaitResult WaitUntil(long deadlineTicks, AutoResetEvent wake, IReplayMonotonicClock clock);
}

internal static class ReplayDeadlineWaiterFactory
{
  public static IReplayDeadlineWaiter Create()
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return new WindowsReplayDeadlineWaiter();
      if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return new MacOsReplayDeadlineWaiter();
    }
    catch (Exception exception)
    {
      return new ManagedReplayDeadlineWaiter(exception.Message);
    }
    return new ManagedReplayDeadlineWaiter(null);
  }
}

internal sealed class ManagedReplayDeadlineWaiter : IReplayDeadlineWaiter
{
  private const long CoarseGuardUs = 2_000L;
  private const long SpinGuardUs = 200L;

  public string Name => "managed-coarse-yield-spin";
  public string FallbackReason { get; }

  public ManagedReplayDeadlineWaiter(string fallbackReason)
  {
    FallbackReason = fallbackReason;
  }

  public ReplayDeadlineWaitResult WaitUntil(long deadlineTicks, AutoResetEvent wake, IReplayMonotonicClock clock)
  {
    while (true)
    {
      long remainingTicks = deadlineTicks - clock.Timestamp;
      if (remainingTicks <= 0)
        return ReplayDeadlineWaitResult.Deadline;
      long remainingUs = ToMicroseconds(remainingTicks, clock.Frequency);
      if (remainingUs > CoarseGuardUs)
      {
        int waitMs = (int)Math.Max(1L, Math.Min(1000L, (remainingUs - CoarseGuardUs) / 1000L));
        if (wake.WaitOne(waitMs))
          return ReplayDeadlineWaitResult.Woken;
        continue;
      }
      if (wake.WaitOne(0))
        return ReplayDeadlineWaitResult.Woken;
      if (remainingUs > SpinGuardUs)
        Thread.Yield();
      else
        Thread.SpinWait(16);
    }
  }

  public void Dispose() { }

  private static long ToMicroseconds(long ticks, long frequency) => (long)(ticks * 1_000_000d / frequency);
}

internal sealed class WindowsReplayDeadlineWaiter : IReplayDeadlineWaiter
{
  private const uint CreateWaitableTimerHighResolution = 0x00000002;
  private const uint TimerAllAccess = 0x001F0003;
  private readonly EventWaitHandle _timerWaitHandle;
  private readonly WaitHandle[] _waitHandles;
  private IntPtr _timer;

  public string Name => "windows-high-resolution-waitable-timer";
  public string FallbackReason => null;

  public WindowsReplayDeadlineWaiter()
  {
    _timer = CreateWaitableTimerEx(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerAllAccess);
    if (_timer == IntPtr.Zero)
      throw new InvalidOperationException("CreateWaitableTimerExW failed. error=" + Marshal.GetLastWin32Error());
    _timerWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
    _timerWaitHandle.SafeWaitHandle = new SafeWaitHandle(_timer, ownsHandle: true);
    _waitHandles = new WaitHandle[2];
  }

  public ReplayDeadlineWaitResult WaitUntil(long deadlineTicks, AutoResetEvent wake, IReplayMonotonicClock clock)
  {
    long remainingTicks = deadlineTicks - clock.Timestamp;
    if (remainingTicks <= 0)
      return ReplayDeadlineWaitResult.Deadline;
    long due100Ns = Math.Max(1L, (long)(remainingTicks * 10_000_000d / clock.Frequency));
    long dueTime = -due100Ns;
    if (!SetWaitableTimerEx(_timer, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0))
      throw new InvalidOperationException("SetWaitableTimerEx failed. error=" + Marshal.GetLastWin32Error());
    _waitHandles[0] = wake;
    _waitHandles[1] = _timerWaitHandle;
    return WaitHandle.WaitAny(_waitHandles) == 0 ? ReplayDeadlineWaitResult.Woken : ReplayDeadlineWaitResult.Deadline;
  }

  public void Dispose()
  {
    _timerWaitHandle?.Dispose();
    _timer = IntPtr.Zero;
  }

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern IntPtr CreateWaitableTimerEx(IntPtr attributes, string name, uint flags, uint access);

  [DllImport("kernel32.dll", SetLastError = true)]
  private static extern bool SetWaitableTimerEx(
    IntPtr timer,
    ref long dueTime,
    int period,
    IntPtr completionRoutine,
    IntPtr arg,
    IntPtr wakeContext,
    uint tolerableDelay
  );
}

internal sealed class MacOsReplayDeadlineWaiter : IReplayDeadlineWaiter
{
  private const long FinalGuardUs = 500L;
  private readonly double _machTicksPerNanosecond;

  public string Name => "macos-mach-wait-until";
  public string FallbackReason => null;

  public MacOsReplayDeadlineWaiter()
  {
    if (MachTimebaseInfo(out MachTimebase timebase) != 0 || timebase.Numer == 0)
      throw new InvalidOperationException("mach_timebase_info failed.");
    _machTicksPerNanosecond = timebase.Denom / (double)timebase.Numer;
  }

  public ReplayDeadlineWaitResult WaitUntil(long deadlineTicks, AutoResetEvent wake, IReplayMonotonicClock clock)
  {
    while (true)
    {
      long remainingTicks = deadlineTicks - clock.Timestamp;
      if (remainingTicks <= 0)
        return ReplayDeadlineWaitResult.Deadline;
      long remainingUs = (long)(remainingTicks * 1_000_000d / clock.Frequency);
      if (remainingUs > FinalGuardUs)
      {
        int waitMs = (int)Math.Max(1L, Math.Min(1000L, (remainingUs - FinalGuardUs) / 1000L));
        if (wake.WaitOne(waitMs))
          return ReplayDeadlineWaitResult.Woken;
        continue;
      }
      if (wake.WaitOne(0))
        return ReplayDeadlineWaitResult.Woken;
      ulong target = MachAbsoluteTime() + (ulong)Math.Max(1d, remainingUs * 1000d * _machTicksPerNanosecond);
      MachWaitUntil(target);
      return ReplayDeadlineWaitResult.Deadline;
    }
  }

  public void Dispose() { }

  [StructLayout(LayoutKind.Sequential)]
  private struct MachTimebase
  {
    public uint Numer;
    public uint Denom;
  }

  [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_absolute_time")]
  private static extern ulong MachAbsoluteTime();

  [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_wait_until")]
  private static extern int MachWaitUntil(ulong deadline);

  [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_timebase_info")]
  private static extern int MachTimebaseInfo(out MachTimebase info);
}
