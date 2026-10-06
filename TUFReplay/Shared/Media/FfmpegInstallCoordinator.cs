using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Composition;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Shared.Media;

internal static class FfmpegInstallCoordinator
{
  public static event Action Changed;

  private static void NotifyChanged()
  {
    Changed?.Invoke();
    UnityMainThread.Post(() =>
    {
      if (!_requested || _installer == null)
        return;
      var state = _installer.Snapshot();
      if (state.Status == "missing")
      {
        _installer.Request();
        return;
      }
      if (state.Available || state.Status == "declined" || state.Status == "cancelled" || state.Status == "failed")
      {
        _requested = false;
        Requesters.Clear();
        if (state.Available && Main.Settings.WebcamEnabled)
          FeatureRegistry.WebcamRecording?.ArmForLevel();
      }
    });
  }

  private static readonly System.Collections.Generic.HashSet<string> Requesters = new();
  private static ManagedFfmpegInstaller _installer;
  private static bool _requested;
  private static int _cameraWaiters;
  private static string _initializationError;

  public static void Initialize()
  {
    try
    {
      _installer = new ManagedFfmpegInstaller(Main.Instance.InstallPath, FfmpegPlatform.Current());
      _installer.Changed += NotifyChanged;
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

  public static FfmpegInstallState Request(string requester = "default")
  {
    Requesters.Add(requester);
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

  public static void CancelPendingRequest(string requester = "default")
  {
    Requesters.Remove(requester);
    CancelIfUnneeded();
  }

  private static void CancelIfUnneeded()
  {
    if (Requesters.Count > 0 || Volatile.Read(ref _cameraWaiters) > 0)
      return;
    string state = Status().Status;
    if (state != "awaiting-consent" && state != "checking")
      return;
    if (state == "awaiting-consent")
      _installer?.Cancel();
    _requested = false;
  }

  public static async Task<string> EnsureAvailableAsync(CancellationToken cancellationToken)
  {
    var installer = _installer ?? throw new IOException(_initializationError ?? "Enable TUFReplay to install FFmpeg.");
    cancellationToken.ThrowIfCancellationRequested();
    if (installer.Snapshot().Available)
      return installer.Snapshot().Path;
    Interlocked.Increment(ref _cameraWaiters);
    var ready = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    Action inspect = () =>
    {
      var state = installer.Snapshot();
      if (state.Available)
        ready.TrySetResult(state.Path);
      else if (state.Status == "declined" || state.Status == "cancelled")
        ready.TrySetException(
          new IOException(
            "FFmpeg installation was skipped. Turn the camera on again and install FFmpeg from the web download center."
          )
        );
      else if (state.Status == "failed")
        ready.TrySetException(new IOException(state.Error));
    };
    installer.Changed += inspect;
    using (cancellationToken.Register(() => ready.TrySetCanceled()))
    {
      try
      {
        UnityMainThread.Post(() =>
        {
          if (cancellationToken.IsCancellationRequested)
            ready.TrySetCanceled();
          else
            RequestCore();
        });
        inspect();
        return await ready.Task.ConfigureAwait(false);
      }
      finally
      {
        installer.Changed -= inspect;
        Interlocked.Decrement(ref _cameraWaiters);
        UnityMainThread.Post(CancelIfUnneeded);
      }
    }
  }

  public static void Shutdown()
  {
    if (_installer != null)
      _installer.Changed -= NotifyChanged;
    _installer?.Dispose();
    _installer = null;
    _requested = false;
    Requesters.Clear();
    _initializationError = null;
  }
}
