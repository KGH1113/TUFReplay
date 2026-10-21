using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TUFReplay.Recording.Input;

internal enum MacOsInputAccess
{
  Granted = 0,
  Denied = 1,
  Unknown = 2,
  Unavailable = 3,
}

internal enum MacOsInputError
{
  None = 0,
  Permission = 1,
  TapCreate = 2,
  RunLoopSource = 3,
  ThreadStart = 4,
  StartTimeout = 5,
}

internal static class MacOsCGEventErrorFormatter
{
  public static string Format(MacOsInputError error, int systemError) =>
    systemError == 0 ? error.ToString() : error + ": system error " + systemError;
}

[Flags]
internal enum MacOsCaptureFaults : uint
{
  None = 0,
  TapTimeout = 1,
  TapDisabled = 2,
  EventDelayed = 4,
}

[StructLayout(LayoutKind.Sequential)]
internal struct MacOsCGEventNativeEvent
{
  public ulong TimestampNs;
  public ulong ModifierFlags;
  public uint KeyCode;
  public byte Down;
  public byte Reserved0;
  public byte Reserved1;
  public byte Reserved2;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MacOsCGEventNativeStats
{
  public ulong CallbackCount;
  public ulong QueuedCount;
  public ulong DroppedCount;
  public ulong RepeatCount;
  public ulong UnmappedCount;
  public uint DeviceCount;
  public uint QueueDepth;
}

internal sealed class MacOsCGEventNativeLibrary
{
  private const uint ExpectedAbiVersion = 3;
  private const int RtldNow = 2;
  private static readonly object LoadGate = new object();
  private static MacOsCGEventNativeLibrary _instance;
  private static string _loadFailure;

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate uint AbiVersionDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int AccessDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate ulong ClockNowDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr CreateDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int ContextResultDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void ContextActionDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  [return: MarshalAs(UnmanagedType.I1)]
  private delegate bool IsRunningDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int WaitDequeueDelegate(IntPtr context, IntPtr events, int capacity, int timeoutMs);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int CopyStateDelegate(IntPtr context, IntPtr state, int capacity);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate ulong TakeDroppedDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate uint TakeFaultsDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void GetStatsDelegate(IntPtr context, out MacOsCGEventNativeStats stats);

  private readonly IntPtr _handle;
  private readonly AccessDelegate _checkAccess;
  private readonly AccessDelegate _requestAccess;
  private readonly ClockNowDelegate _clockNow;
  private readonly CreateDelegate _create;
  private readonly ContextResultDelegate _start;
  private readonly ContextActionDelegate _stop;
  private readonly ContextActionDelegate _destroy;
  private readonly IsRunningDelegate _isRunning;
  private readonly ContextResultDelegate _lastError;
  private readonly ContextResultDelegate _lastSystemError;
  private readonly WaitDequeueDelegate _waitDequeue;
  private readonly CopyStateDelegate _copyState;
  private readonly TakeDroppedDelegate _takeDropped;
  private readonly TakeFaultsDelegate _takeFaults;
  private readonly GetStatsDelegate _getStats;

  private MacOsCGEventNativeLibrary(string path)
  {
    _handle = dlopen(path, RtldNow);
    if (_handle == IntPtr.Zero)
    {
      IntPtr error = dlerror();
      throw new DllNotFoundException(
        path + ": " + (error == IntPtr.Zero ? "dlopen failed" : Marshal.PtrToStringAnsi(error))
      );
    }

    AbiVersionDelegate abiVersion = Bind<AbiVersionDelegate>("tufreplay_input_abi_version");
    if (abiVersion() != ExpectedAbiVersion)
      throw new InvalidOperationException("macOS native input ABI version mismatch.");
    _checkAccess = Bind<AccessDelegate>("tufreplay_input_check_access");
    _requestAccess = Bind<AccessDelegate>("tufreplay_input_request_access");
    _clockNow = Bind<ClockNowDelegate>("tufreplay_input_clock_now_ns");
    _create = Bind<CreateDelegate>("tufreplay_input_create");
    _start = Bind<ContextResultDelegate>("tufreplay_input_start");
    _stop = Bind<ContextActionDelegate>("tufreplay_input_stop");
    _destroy = Bind<ContextActionDelegate>("tufreplay_input_destroy");
    _isRunning = Bind<IsRunningDelegate>("tufreplay_input_is_running");
    _lastError = Bind<ContextResultDelegate>("tufreplay_input_last_error");
    _lastSystemError = Bind<ContextResultDelegate>("tufreplay_input_last_system_error");
    _waitDequeue = Bind<WaitDequeueDelegate>("tufreplay_input_wait_dequeue");
    _copyState = Bind<CopyStateDelegate>("tufreplay_input_copy_state");
    _takeDropped = Bind<TakeDroppedDelegate>("tufreplay_input_take_dropped");
    _takeFaults = Bind<TakeFaultsDelegate>("tufreplay_input_take_faults");
    _getStats = Bind<GetStatsDelegate>("tufreplay_input_get_stats");
  }

  public static bool TryLoad(out MacOsCGEventNativeLibrary library, out string failure)
  {
    lock (LoadGate)
    {
      if (_instance != null)
      {
        library = _instance;
        failure = null;
        return true;
      }
      if (_loadFailure != null)
      {
        library = null;
        failure = _loadFailure;
        return false;
      }

      try
      {
        string configured = Environment.GetEnvironmentVariable("TUFREPLAY_MAC_INPUT_LIBRARY");
        string path = !string.IsNullOrWhiteSpace(configured)
          ? configured
          : Path.Combine(Main.Instance.PayloadPath, "libTUFReplayInput.dylib");
        if (!File.Exists(path))
          throw new FileNotFoundException("macOS native input shim is missing.", path);
        _instance = new MacOsCGEventNativeLibrary(path);
        library = _instance;
        failure = null;
        return true;
      }
      catch (Exception exception)
      {
        _loadFailure = "dylib_missing_or_invalid: " + exception.Message;
        library = null;
        failure = _loadFailure;
        return false;
      }
    }
  }

  public MacOsInputAccess CheckAccess() => (MacOsInputAccess)_checkAccess();

  public MacOsInputAccess RequestAccess() => (MacOsInputAccess)_requestAccess();

  public ulong ClockNowNs() => _clockNow();

  public IntPtr Create() => _create();

  public MacOsInputError Start(IntPtr context) => (MacOsInputError)_start(context);

  public void Stop(IntPtr context) => _stop(context);

  public void Destroy(IntPtr context) => _destroy(context);

  public bool IsRunning(IntPtr context) => context != IntPtr.Zero && _isRunning(context);

  public MacOsInputError LastError(IntPtr context) => (MacOsInputError)_lastError(context);

  public int LastSystemError(IntPtr context) => _lastSystemError(context);

  public int WaitDequeue(IntPtr context, IntPtr events, int capacity, int timeoutMs) =>
    _waitDequeue(context, events, capacity, timeoutMs);

  public int CopyState(IntPtr context, IntPtr state, int capacity) => _copyState(context, state, capacity);

  public ulong TakeDropped(IntPtr context) => _takeDropped(context);

  public MacOsCaptureFaults TakeFaults(IntPtr context) => (MacOsCaptureFaults)_takeFaults(context);

  public MacOsCGEventNativeStats GetStats(IntPtr context)
  {
    _getStats(context, out MacOsCGEventNativeStats stats);
    return stats;
  }

  private T Bind<T>(string name)
    where T : class
  {
    IntPtr symbol = dlsym(_handle, name);
    if (symbol == IntPtr.Zero)
      throw new EntryPointNotFoundException(name);
    return (T)(object)Marshal.GetDelegateForFunctionPointer(symbol, typeof(T));
  }

  [DllImport("libdl")]
  private static extern IntPtr dlopen(string path, int mode);

  [DllImport("libdl")]
  private static extern IntPtr dlsym(IntPtr handle, string symbol);

  [DllImport("libdl")]
  private static extern IntPtr dlerror();
}
