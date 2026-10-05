using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Composition;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Shared.Media;

internal static class FfmpegInstallCoordinator
{
  private static ManagedFfmpegInstaller _installer;
  private static bool _requested;
  private static bool _rendererRequested;
  private static int _cameraWaiters;
  private static string _initializationError;
  private static long _nextRefresh;

  public static void Initialize()
  {
    try
    {
      _installer = new ManagedFfmpegInstaller(Main.Instance.InstallPath, FfmpegPlatform.Current());
    }
    catch (Exception error)
    {
      _initializationError = error.Message;
    }
  }

  public static FfmpegInstallState Status() =>
    _installer?.Snapshot()
    ?? new FfmpegInstallState
    {
      Status = "failed",
      Error = _initializationError ?? "Enable TUFReplay to install FFmpeg.",
    };

  public static FfmpegInstallState Request()
  {
    _rendererRequested = true;
    return RequestCore();
  }

  private static FfmpegInstallState RequestCore()
  {
    _requested = true;
    _installer?.Request();
    return Status();
  }

  public static FfmpegInstallState Confirm()
  {
    RequestCore();
    _installer?.Confirm();
    return Status();
  }

  public static FfmpegInstallState Decline()
  {
    _installer?.Decline();
    return Status();
  }

  public static FfmpegInstallState Cancel()
  {
    _installer?.Cancel();
    return Status();
  }

  public static void CancelPendingRequest()
  {
    _rendererRequested = false;
    CancelIfUnneeded();
  }

  private static void CancelIfUnneeded()
  {
    if (_rendererRequested || Volatile.Read(ref _cameraWaiters) > 0)
      return;
    string state = Status().Status;
    if (state != "awaiting-consent" && state != "checking")
      return;
    if (state == "awaiting-consent")
      _installer?.Cancel();
    _requested = false;
  }

  public static async Task<string> EnsureAvailableAsync(Func<bool> stillNeeded)
  {
    var installer = _installer ?? throw new IOException(_initializationError ?? "Enable TUFReplay to install FFmpeg.");
    if (installer.Snapshot().Available)
      return installer.Snapshot().Path;
    Interlocked.Increment(ref _cameraWaiters);
    try
    {
      var requested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      UnityMainThread.Post(() =>
      {
        if (stillNeeded())
        {
          RequestCore();
          requested.TrySetResult(true);
        }
        else
          requested.TrySetResult(false);
      });
      if (!await requested.Task.ConfigureAwait(false))
        throw new OperationCanceledException();
      while (stillNeeded())
      {
        var state = installer.Snapshot();
        if (state.Available)
          return state.Path;
        if (state.Status == "declined" || state.Status == "cancelled")
          throw new IOException(
            "FFmpeg installation was skipped. Turn the camera on again and install FFmpeg from the web download center."
          );
        if (state.Status == "failed")
          throw new IOException(state.Error);
        await Task.Delay(100).ConfigureAwait(false);
      }
      throw new OperationCanceledException();
    }
    finally
    {
      Interlocked.Decrement(ref _cameraWaiters);
      if (!stillNeeded())
        UnityMainThread.Post(CancelIfUnneeded);
    }
  }

  public static void Tick()
  {
    if (!_requested || _installer == null)
      return;
    long now = Stopwatch.GetTimestamp();
    if (now < _nextRefresh)
      return;
    _nextRefresh = now + Stopwatch.Frequency / 10;
    var state = _installer.Snapshot();
    if (state.Status == "checking")
      return;
    if (state.Status == "missing")
      state = _installer.Request();
    if (state.Available || state.Status == "declined" || state.Status == "cancelled")
    {
      _requested = false;
      _rendererRequested = false;
      if (state.Available && Main.Settings.WebcamEnabled)
        FeatureRegistry.WebcamRecording?.ArmForLevel();
      return;
    }
  }

  public static void Shutdown()
  {
    _installer?.Dispose();
    _installer = null;
    _requested = false;
    _rendererRequested = false;
    _initializationError = null;
  }
}
