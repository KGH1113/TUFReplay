using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TUFReplay.Shared.Native;

[StructLayout(LayoutKind.Sequential, Size = 32)]
internal struct WindowsNativeKeyEvent
{
  public long QpcTicks;
  public uint TimeMs;
  public uint VirtualKey;
  public uint ScanCode;
  public uint Flags;
  public byte Down;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowsNativeInputStats
{
  public ulong CallbackCount;
  public ulong QueuedCount;
  public ulong DroppedCount;
  public uint QueueDepth;
  public uint Reserved;
}

internal enum WindowsNativeInputError
{
  None = 0,
  HookInstall = 1,
  ThreadStart = 2,
  StartTimeout = 3,
}

internal enum WindowsAudioTransport
{
  Unknown = 0,
  Other = 1,
  Bluetooth = 2,
}

// TUFReplayNative.dll: the low-level keyboard hook and audio endpoint queries that
// must not run managed code (see TUFReplay.Native.Windows).
internal sealed class WindowsNativeLibrary
{
  public const string FileName = "TUFReplayNative.dll";
  private const uint ExpectedAbiVersion = 1;
  private static readonly object LoadGate = new object();
  private static WindowsNativeLibrary _instance;
  private static string _loadFailure;

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate uint AbiVersionDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr CreateDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int ContextResultDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void ContextActionDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int WaitDequeueDelegate(IntPtr context, IntPtr events, int capacity, int timeoutMs);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate ulong TakeDroppedDelegate(IntPtr context);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void GetStatsDelegate(IntPtr context, out WindowsNativeInputStats stats);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
  private delegate int AudioTransportDelegate([MarshalAs(UnmanagedType.LPWStr)] string name);

  private readonly NativeModule _module;
  private readonly CreateDelegate _create;
  private readonly ContextResultDelegate _start;
  private readonly ContextActionDelegate _stop;
  private readonly ContextActionDelegate _destroy;
  private readonly ContextResultDelegate _isRunning;
  private readonly ContextResultDelegate _lastError;
  private readonly ContextResultDelegate _lastSystemError;
  private readonly WaitDequeueDelegate _waitDequeue;
  private readonly TakeDroppedDelegate _takeDropped;
  private readonly GetStatsDelegate _getStats;
  private readonly AudioTransportDelegate _audioTransport;

  private WindowsNativeLibrary(string path)
  {
    _module = NativeModule.Open(path);
    if (Bind<AbiVersionDelegate>("tufreplay_win_native_abi_version")() != ExpectedAbiVersion)
      throw new InvalidOperationException("Windows native library ABI version mismatch.");
    _create = Bind<CreateDelegate>("tufreplay_win_input_create");
    _start = Bind<ContextResultDelegate>("tufreplay_win_input_start");
    _stop = Bind<ContextActionDelegate>("tufreplay_win_input_stop");
    _destroy = Bind<ContextActionDelegate>("tufreplay_win_input_destroy");
    _isRunning = Bind<ContextResultDelegate>("tufreplay_win_input_is_running");
    _lastError = Bind<ContextResultDelegate>("tufreplay_win_input_last_error");
    _lastSystemError = Bind<ContextResultDelegate>("tufreplay_win_input_last_system_error");
    _waitDequeue = Bind<WaitDequeueDelegate>("tufreplay_win_input_wait_dequeue");
    _takeDropped = Bind<TakeDroppedDelegate>("tufreplay_win_input_take_dropped");
    _getStats = Bind<GetStatsDelegate>("tufreplay_win_input_get_stats");
    _audioTransport = Bind<AudioTransportDelegate>("tufreplay_win_audio_capture_transport");
  }

  public static bool TryLoad(out WindowsNativeLibrary library, out string failure)
  {
    lock (LoadGate)
    {
      if (_instance != null || _loadFailure != null)
      {
        library = _instance;
        failure = _loadFailure;
        return library != null;
      }

      try
      {
        string configured = Environment.GetEnvironmentVariable("TUFREPLAY_WINDOWS_NATIVE_LIBRARY");
        string path = !string.IsNullOrWhiteSpace(configured)
          ? configured
          : Path.Combine(Main.Instance.PayloadPath, FileName);
        if (!File.Exists(path))
          throw new FileNotFoundException("The Windows native library is missing.", path);
        _instance = new WindowsNativeLibrary(path);
      }
      catch (Exception exception)
      {
        _loadFailure = "native_library_unavailable: " + exception.Message;
      }
      library = _instance;
      failure = _loadFailure;
      return library != null;
    }
  }

  public IntPtr CreateInput() => _create();

  public WindowsNativeInputError StartInput(IntPtr context) => (WindowsNativeInputError)_start(context);

  public void StopInput(IntPtr context) => _stop(context);

  public void DestroyInput(IntPtr context) => _destroy(context);

  public bool IsInputRunning(IntPtr context) => context != IntPtr.Zero && _isRunning(context) != 0;

  public WindowsNativeInputError LastInputError(IntPtr context) => (WindowsNativeInputError)_lastError(context);

  public int LastInputSystemError(IntPtr context) => _lastSystemError(context);

  public int WaitDequeue(IntPtr context, IntPtr events, int capacity, int timeoutMs) =>
    _waitDequeue(context, events, capacity, timeoutMs);

  public ulong TakeDropped(IntPtr context) => _takeDropped(context);

  public WindowsNativeInputStats GetStats(IntPtr context)
  {
    _getStats(context, out WindowsNativeInputStats stats);
    return stats;
  }

  // A null name queries the default recording endpoint.
  public WindowsAudioTransport CaptureTransport(string endpointName) =>
    (WindowsAudioTransport)_audioTransport(endpointName);

  private T Bind<T>(string name)
    where T : class => _module.Bind<T>(name);
}
