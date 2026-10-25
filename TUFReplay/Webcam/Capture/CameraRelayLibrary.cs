using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using TUFReplay.Shared.Native;

namespace TUFReplay.Webcam.Capture;

internal enum CameraRelayError
{
  None = 0,
  StoppedSending = 1,
  ResolutionUnavailable = 2,
  ResolutionChanged = 3,
  FellBehind = 4,
  Timestamp = 5,
  OutOfMemory = 6,
  Stopped = 7,
  InvalidArgument = 8,
}

[StructLayout(LayoutKind.Sequential)]
internal struct CameraRelayConfig
{
  public long FrameHandle;
  public long LogHandle;
  public int MaxWidth;
  public int MaxHeight;
  public long ClockUnixUs;
  public long ClockTicks;
  public long TicksFrequency;
  public int PoolCapacity;
  public int TimestampTimeoutMs;
  public IntPtr PreviewPath;
  public int PreviewMaxWidth;
  public int PreviewMaxHeight;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CameraRelayState
{
  public int Ready;
  public int Error;
  public int Width;
  public int Height;
  public long InputRateNumerator;
  public long InputRateDenominator;
  public long FramesCaptured;
  public long PreviewsPublished;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CameraRelayRecordingStats
{
  public long WrittenFrames;
  public long FirstTimestampTicks;
  public long DurationUs;
  public long DroppedFrames;
  public int WriterFinished;
  public int WriteFailed;
  public int OutputClosedEarly;
  public int CaptureFailed;
}

// Binding for the camera relay in TUFReplayNative.dll (TUFReplay.Native.Windows).
// The C# tests load the same relay built for the host from TUFREPLAY_CAMERA_NATIVE_LIBRARY.
internal sealed class CameraRelayLibrary
{
  private const uint ExpectedAbiVersion = 1;
  private static readonly object LoadGate = new object();
  private static CameraRelayLibrary _instance;
  private static string _loadFailure;

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate uint AbiVersionDelegate();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr CreateDelegate(ref CameraRelayConfig config);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int LastCreateErrorDelegate(out int systemError);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int WaitReadyDelegate(IntPtr camera, int timeoutMs);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void GetStateDelegate(IntPtr camera, out CameraRelayState state);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int GetDiagnosticsDelegate(IntPtr camera, byte[] destination, int capacity);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void ActionDelegate(IntPtr handle);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int DestroyDelegate(IntPtr handle, int timeoutMs);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr AttachDelegate(
    IntPtr camera,
    long boundaryTicks,
    long nowTicks,
    int frameRate,
    int queueCapacity
  );

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SetOutputDelegate(IntPtr recording, long outputHandle);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void GetStatsDelegate(IntPtr recording, out CameraRelayRecordingStats stats);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int FinishDelegate(IntPtr recording, int timeoutMs, out CameraRelayRecordingStats stats);

  private readonly CreateDelegate _create;
  private readonly LastCreateErrorDelegate _lastCreateError;
  private readonly WaitReadyDelegate _waitReady;
  private readonly GetStateDelegate _getState;
  private readonly GetDiagnosticsDelegate _getDiagnostics;
  private readonly ActionDelegate _stop;
  private readonly DestroyDelegate _destroy;
  private readonly AttachDelegate _attach;
  private readonly SetOutputDelegate _setOutput;
  private readonly GetStatsDelegate _getStats;
  private readonly ActionDelegate _seal;
  private readonly FinishDelegate _finish;
  private readonly ActionDelegate _cancel;
  private readonly DestroyDelegate _destroyRecording;

  private CameraRelayLibrary(NativeModule module)
  {
    if (module.Bind<AbiVersionDelegate>("tufreplay_camera_abi_version")() != ExpectedAbiVersion)
      throw new InvalidOperationException("Camera relay ABI version mismatch.");
    _create = module.Bind<CreateDelegate>("tufreplay_camera_create");
    _lastCreateError = module.Bind<LastCreateErrorDelegate>("tufreplay_camera_last_create_error");
    _waitReady = module.Bind<WaitReadyDelegate>("tufreplay_camera_wait_ready");
    _getState = module.Bind<GetStateDelegate>("tufreplay_camera_get_state");
    _getDiagnostics = module.Bind<GetDiagnosticsDelegate>("tufreplay_camera_get_diagnostics");
    _stop = module.Bind<ActionDelegate>("tufreplay_camera_stop");
    _destroy = module.Bind<DestroyDelegate>("tufreplay_camera_destroy");
    _attach = module.Bind<AttachDelegate>("tufreplay_camera_recording_attach");
    _setOutput = module.Bind<SetOutputDelegate>("tufreplay_camera_recording_set_output");
    _getStats = module.Bind<GetStatsDelegate>("tufreplay_camera_recording_get_stats");
    _seal = module.Bind<ActionDelegate>("tufreplay_camera_recording_seal");
    _finish = module.Bind<FinishDelegate>("tufreplay_camera_recording_finish");
    _cancel = module.Bind<ActionDelegate>("tufreplay_camera_recording_cancel");
    _destroyRecording = module.Bind<DestroyDelegate>("tufreplay_camera_recording_destroy");
  }

  public static CameraRelayLibrary Load()
  {
    lock (LoadGate)
    {
      if (_instance != null)
        return _instance;
      if (_loadFailure != null)
        throw new IOException(_loadFailure);
      try
      {
        string configured = Environment.GetEnvironmentVariable("TUFREPLAY_CAMERA_NATIVE_LIBRARY");
        string path = !string.IsNullOrWhiteSpace(configured)
          ? configured
          : Path.Combine(Main.Instance.PayloadPath, WindowsNativeLibrary.FileName);
        if (!File.Exists(path))
          throw new FileNotFoundException("The camera relay library is missing.", path);
        _instance = new CameraRelayLibrary(NativeModule.Open(path));
        return _instance;
      }
      catch (Exception exception)
      {
        _loadFailure =
          "TUFReplay's camera library could not load. Reinstall TUFReplay and try again. (" + exception.Message + ")";
        throw new IOException(_loadFailure, exception);
      }
    }
  }

  // Returns the relay, or zero with the failed stage in failure.
  public IntPtr Create(CameraRelayConfig config, string previewPath, out string failure)
  {
    failure = null;
    IntPtr path = IntPtr.Zero;
    try
    {
      if (previewPath != null)
      {
        byte[] bytes = Encoding.UTF8.GetBytes(previewPath + "\0");
        path = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, path, bytes.Length);
      }
      config.PreviewPath = path;
      IntPtr camera = _create(ref config);
      if (camera == IntPtr.Zero)
      {
        int stage = _lastCreateError(out int systemError);
        string name =
          stage == 2 ? "capture_handles"
          : stage == 3 ? "preview_mapping"
          : stage == 4 ? "threads"
          : "config";
        failure = name + " (" + systemError + ")";
      }
      return camera;
    }
    finally
    {
      if (path != IntPtr.Zero)
        Marshal.FreeHGlobal(path);
    }
  }

  public int WaitReady(IntPtr camera, int timeoutMs) => _waitReady(camera, timeoutMs);

  public CameraRelayState GetState(IntPtr camera)
  {
    _getState(camera, out CameraRelayState state);
    return state;
  }

  public string GetDiagnostics(IntPtr camera)
  {
    var buffer = new byte[16 * 512];
    int count = _getDiagnostics(camera, buffer, buffer.Length);
    return Encoding.UTF8.GetString(buffer, 0, Math.Max(0, Math.Min(count, buffer.Length)));
  }

  public void Stop(IntPtr camera) => _stop(camera);

  public bool Destroy(IntPtr camera, int timeoutMs) => _destroy(camera, timeoutMs) != 0;

  public IntPtr Attach(IntPtr camera, long boundaryTicks, long nowTicks, int frameRate, int queueCapacity) =>
    _attach(camera, boundaryTicks, nowTicks, frameRate, queueCapacity);

  public CameraRelayError SetOutput(IntPtr recording, long outputHandle) =>
    (CameraRelayError)_setOutput(recording, outputHandle);

  public CameraRelayRecordingStats GetStats(IntPtr recording)
  {
    _getStats(recording, out CameraRelayRecordingStats stats);
    return stats;
  }

  public void Seal(IntPtr recording) => _seal(recording);

  public bool Finish(IntPtr recording, int timeoutMs, out CameraRelayRecordingStats stats) =>
    _finish(recording, timeoutMs, out stats) != 0;

  public void Cancel(IntPtr recording) => _cancel(recording);

  public bool DestroyRecording(IntPtr recording, int timeoutMs) => _destroyRecording(recording, timeoutMs) != 0;

  // The relay duplicates the handle; the stream keeps owning its own copy.
  public static long OsHandle(Stream stream)
  {
    switch (stream)
    {
      case FileStream file:
        return file.SafeFileHandle.DangerousGetHandle().ToInt64();
      case PipeStream pipe:
        return pipe.SafePipeHandle.DangerousGetHandle().ToInt64();
      default:
        throw new NotSupportedException("The camera process stream has no operating-system handle.");
    }
  }
}
