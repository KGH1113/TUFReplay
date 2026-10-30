using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Threading;
using TUFReplay.Shared.Timing;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Ipc;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;
using UnityEngine.SceneManagement;

namespace TUFReplay.Webcam.Recording;

public sealed class WebcamRecordingFeature
{
  public event Action StateChanged;

  public void NotifyStateChanged() => StateChanged?.Invoke();

  private readonly object _gate = new object();
  private Task _work = Task.CompletedTask;
  private IWebcamCaptureBackend _backend;
  private List<WebcamDevice> _devices = new List<WebcamDevice>();
  private bool _active;
  private bool _armed;
  private bool _arming;
  private bool _recording;
  private bool _levelOpen;
  private bool _refreshQueued;
  private DateTime _lastDeviceRefreshUtc = DateTime.MinValue;
  private int _finalizing;
  private int _generation;
  private CancellationTokenSource _armCancellation = new CancellationTokenSource();
  private IWebcamCaptureBackend _runBackend;
  private int _runGeneration;
  private string _error;
  private System.Threading.Timer _retention;

  public WebcamRecordingStore Store { get; private set; }
  public CameraPreviewBuffer Preview => _backend?.Preview;
  public bool IsReady
  {
    get
    {
      lock (_gate)
        return _armed;
    }
  }
  public bool Supported =>
    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

  public void Enable()
  {
    _active = true;
    SceneManager.activeSceneChanged += OnActiveSceneChanged;
    try
    {
      Store = new WebcamRecordingStore(Path.Combine(Main.Instance.InstallPath, "Data", "Webcam"));

      Queue(
        "storage.initialize",
        () =>
        {
          Store.CleanupInterruptedCaptures();
          Cleanup();
          return Task.CompletedTask;
        }
      );
      // The scan reads every recording's metadata; keep it out of judged gameplay.
      _retention = new System.Threading.Timer(
        _ =>
          GameplayIdleGate.Run(() =>
            Queue(
              "storage.retention",
              () =>
              {
                Cleanup();
                return Task.CompletedTask;
              }
            )
          ),
        null,
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(1)
      );
      if (Main.Settings.WebcamEnabled)
        ArmForLevel();
    }
    catch (Exception exception)
    {
      SetError(exception, "feature.enable");
    }
  }

  public void Disable()
  {
    SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    lock (_gate)
    {
      _active = false;
      _levelOpen = false;
      _armed = false;
      _arming = false;
      _generation++;
      _armCancellation.Cancel();
    }
    _retention?.Dispose();
    Queue(
      "feature.disable",
      async () =>
      {
        try
        {
          if (_backend != null)
            await _backend.DisarmAsync();
        }
        finally
        {
          _backend?.Dispose();
          _backend = null;
        }
      }
    );
  }

  public WebcamStateDto GetState(bool refreshDevices = false, bool forceDeviceRefresh = false)
  {
    if (refreshDevices && Supported)
      RefreshDevices(forceDeviceRefresh);
    TUFReplaySetting settings = Main.Settings;
    lock (_gate)
      return new WebcamStateDto
      {
        Supported = Supported,
        Enabled = settings.WebcamEnabled,
        CaptureLocked = _recording || _finalizing > 0,
        Backend =
          RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "avfoundation"
          : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg"
          : "unsupported",
        Status =
          !settings.WebcamEnabled ? "off"
          : _error != null ? "error"
          : _recording ? "recording"
          : _finalizing > 0 ? "saving"
          : _armed ? "ready"
          : "warming",
        Error = _error,
        Devices = new List<WebcamDevice>(_devices),
        SelectedDeviceId = settings.WebcamDeviceId,
        Quality = settings.WebcamQuality,
        OffsetMs = settings.WebcamOffsetMs,
        RecordingLimitMb = settings.WebcamRecordingLimitMb,
        RetentionDays = settings.WebcamRetentionDays,
        PlaybackVisible = settings.WebcamPlaybackVisible,
        LiveVisible = settings.WebcamLiveVisible,
        Mirror = settings.WebcamMirror,
        FlipVertical = settings.WebcamFlipVertical,
        OverlayX = settings.WebcamOverlayX,
        OverlayY = settings.WebcamOverlayY,
        OverlayWidth = settings.WebcamOverlayWidth,
        Crop = WebcamCropRect.Get(settings),
      };
  }

  public bool TryUpdate(WebcamSettingsPatch patch, out string message)
  {
    message = null;
    lock (_gate)
    {
      if (!_active || (patch.ChangesCapture && (_recording || _finalizing > 0)))
      {
        message = "Finish the current recording before changing the camera or recording quality.";
        return false;
      }
    }
    if (!Supported && patch.RearmsCapture)
    {
      message = "Camera recording is available on macOS and Windows.";
      return false;
    }
    TUFReplaySetting settings = Main.Settings;
    var previous = JsonConvert.DeserializeObject<TUFReplaySetting>(JsonConvert.SerializeObject(settings));
    try
    {
      patch.ApplyTo(settings);
      TUFReplaySettingStore.Save();
    }
    catch (Exception exception)
    {
      CopyWebcamSettings(previous, settings);
      Main.Instance?.LogException("Webcam/Settings", exception);
      message = "Camera settings could not be saved. Check available disk space and try again.";
      return false;
    }
    if (patch.RearmsCapture)
    {
      lock (_gate)
      {
        _armed = false;
        _arming = false;
        _error = null;
        _generation++;
        _armCancellation.Cancel();
      }
      Queue(
        "settings.rearm.disarm",
        async () =>
        {
          if (_backend != null)
            await _backend.DisarmAsync();
          _backend?.Dispose();
          _backend = null;
        }
      );
      if (settings.WebcamEnabled)
        ArmForLevel();
    }
    if (patch.ChangesCapture)
      Queue(
        "settings.storage.cleanup",
        () =>
        {
          Cleanup(patch.ChangesRetention);
          return Task.CompletedTask;
        }
      );
    NotifyStateChanged();
    return true;
  }

  public void ArmForLevel()
  {
    if (!Supported || !_active || !Main.Settings.WebcamEnabled)
      return;
    string device = Main.Settings.WebcamDeviceId;
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality(Main.Settings.WebcamQuality);
    int generation;
    CancellationToken armToken;
    lock (_gate)
    {
      if (_recording || _armed || _arming)
        return;
      generation = ++_generation;
      _armCancellation.Cancel();
      _armCancellation = new CancellationTokenSource();
      armToken = _armCancellation.Token;
      _armed = false;
      _arming = true;
      _error = null;
    }
    Queue(
      "capture.arm",
      async () =>
      {
        try
        {
          if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            await TUFReplay.Shared.Media.FfmpegInstallCoordinator.EnsureAvailableAsync(armToken);
          if (!_active || generation != _generation)
            return;
          EnsureBackend();

          await _backend.ArmAsync(device, profile, Store.CaptureDirectory);
          DateTime deadline = DateTime.UtcNow.AddSeconds(10);
          while (
            _backend.Preview?.HasFrame != true && DateTime.UtcNow < deadline && generation == _generation && _active
          )
            await Task.Delay(25);
          if (_backend.Preview?.HasFrame != true && generation == _generation && _active)
          {
            throw new IOException("The camera is not sending video. Reconnect it or choose another camera.");
          }
          string warning = null;
          lock (_gate)
            if (_active && generation == _generation)
            {
              _armed = true;
              warning = _backend.CaptureWarning;
            }
          await ApplyLevelEncoder();
          if (!string.IsNullOrEmpty(warning))
            UnityMainThread.Post(() =>
            {
              lock (_gate)
                if (!_active || !_armed || generation != _generation)
                  return;
              ReplayTimelineHud.ShowNotificationToast("Camera frame rate reduced", warning);
            });
        }
        finally
        {
          lock (_gate)
            if (generation == _generation)
              _arming = false;
        }
      }
    );
  }

  // A level was opened (true) or left (false). While one is open, the next run's
  // encoder session stays open, so its video starts with the run instead of
  // after a hardware encoder's multi-second startup.
  public void SetLevelOpen(bool open)
  {
    lock (_gate)
    {
      if (_levelOpen == open || (open && !_active))
        return;
      _levelOpen = open;
    }
    Queue(open ? "capture.encoder.ready" : "capture.encoder.release", ApplyLevelEncoder);
  }

  private void OnActiveSceneChanged(Scene previous, Scene next)
  {
    // Custom levels are edited and played in the editor scene.
    if (next.name != "scnEditor")
      SetLevelOpen(false);
  }

  private Task ApplyLevelEncoder()
  {
    IWebcamCaptureBackend backend;
    bool open;
    lock (_gate)
    {
      if (!_active || !_armed || _backend == null)
        return Task.CompletedTask;
      backend = _backend;
      open = _levelOpen;
    }
    TUFReplaySetting settings = Main.Settings;
    return open && settings.AutoRecord
      ? backend.KeepEncoderReadyAsync(settings.WebcamRecordingLimitMb * 1024L * 1024)
      : backend.ReleaseEncoderAsync();
  }

  public bool BeginRun(string runId, long startTimestampTicks = 0)
  {
    IWebcamCaptureBackend backend;
    int generation;
    lock (_gate)
    {
      if (!_active || !Main.Settings.WebcamEnabled || !_armed || _backend == null || _recording)
      {
        return false;
      }
      _recording = true;
      backend = _runBackend = _backend;
      generation = _runGeneration = _generation;
    }
    long maxBytes = Main.Settings.WebcamRecordingLimitMb * 1024L * 1024;
    Queue(
      "capture.begin",
      async () =>
      {
        // Readiness can change while this operation waits behind camera work.
        // Keep this run bound to the backend that was ready at its start.
        lock (_gate)
          if (!_active || !_armed || generation != _generation || !ReferenceEquals(backend, _backend))
            return;

        await backend.BeginAsync(runId, Store.TemporaryPath(runId), maxBytes, startTimestampTicks);
        lock (_gate)
          _error = null;
      }
    );
    return true;
  }

  public void EndRun(WebcamCaptureTimeline timeline, Action<WebcamRecording> completed)
  {
    IWebcamCaptureBackend backend;
    int generation;
    lock (_gate)
    {
      if (!_recording)
      {
        completed?.Invoke(null);
        return;
      }
      _recording = false;
      _finalizing++;
      backend = _runBackend;
      generation = _runGeneration;
      _runBackend = null;
    }
    Queue(
      "capture.end",
      () =>
      {
        bool current;
        lock (_gate)
          current = backend != null && generation == _generation && ReferenceEquals(backend, _backend);
        Task<WebcamRecording> saving;
        try
        {
          saving = current ? backend.EndAsync() : Task.FromResult<WebcamRecording>(null);
        }
        catch (Exception exception)
        {
          saving = Task.FromException<WebcamRecording>(exception);
        }
        // Ending seals the run at once. A retry's recording begins next in the
        // queue instead of waiting for this video to finish saving.
        _ = CompleteRunAsync(saving, timeline, completed);
        return Task.CompletedTask;
      }
    );
  }

  private async Task CompleteRunAsync(
    Task<WebcamRecording> saving,
    WebcamCaptureTimeline timeline,
    Action<WebcamRecording> completed
  )
  {
    WebcamRecording recording = null;
    try
    {
      recording = await saving;
      if (recording != null && timeline?.TryApplyTo(recording) != true)
      {
        // A run can end before its game clock advances. The camera is still
        // healthy; discard this video without disarming capture.
        WebcamRecordingStore.Discard(recording);
        recording = null;
      }
      completed?.Invoke(recording);
    }
    catch (Exception exception)
    {
      WebcamRecordingStore.Discard(recording);
      SetError(exception, "capture.end");
      completed?.Invoke(null);
    }
    finally
    {
      lock (_gate)
        _finalizing--;
      NotifyStateChanged();
    }
  }

  public void Persist(WebcamRecording recording)
  {
    if (recording == null)
      return;
    long recordingLimit = Main.Settings.WebcamRecordingLimitMb * 1024L * 1024;
    int retention = Main.Settings.WebcamRetentionDays;
    Queue(
      "storage.persist",
      () =>
      {
        try
        {
          bool saved = Store.Save(recording, recordingLimit, retention);
          if (saved)
            TUFReplay.Activity.Management.RunLocks.KeepCameraRecordingIfLocked(recording.RunId);
          TUFReplay.Activity.Tracking.ActivityChanges.Notify(recording.RunId);

          if (!saved || recording.SizeLimited)
            UnityMainThread.Post(() =>
              ReplayTimelineHud.ShowNotificationToast(
                "Camera recording limit",
                saved
                  ? "The camera video reached its recording limit. Microphone audio and replay recording continued. Increase the per-recording limit for longer videos."
                  : "This camera video exceeded its recording limit. Increase the per-recording limit for the next run."
              )
            );
        }
        catch
        {
          WebcamRecordingStore.Discard(recording);
          throw;
        }
        return Task.CompletedTask;
      }
    );
  }

  public void Disarm()
  {
    lock (_gate)
    {
      _armed = false;
      _arming = false;
      _generation++;
      _armCancellation.Cancel();
    }
    Queue(
      "capture.disarm",
      async () =>
      {
        if (_backend != null)
          await _backend.DisarmAsync();
      }
    );
  }

  public void RefreshDevices(bool force = true)
  {
    lock (_gate)
    {
      if (!_active || _refreshQueued || (!force && DateTime.UtcNow - _lastDeviceRefreshUtc < TimeSpan.FromSeconds(30)))
        return;
      _refreshQueued = true;
    }
    Queue(
      "devices.refresh",
      async () =>
      {
        try
        {
          if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !TUFReplay.Shared.Media.FfmpegInstallCoordinator.Status().Available
          )
            return;
          EnsureBackend();
          List<WebcamDevice> devices = await _backend.ListDevicesAsync();

          lock (_gate)
            _devices = devices;
        }
        finally
        {
          lock (_gate)
          {
            _refreshQueued = false;
            _lastDeviceRefreshUtc = DateTime.UtcNow;
          }
        }
      }
    );
  }

  private void EnsureBackend()
  {
    if (_backend != null)
      return;
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      _backend = new MacOsWebcamCaptureBackend();
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      _backend = new FfmpegWebcamCaptureBackend(TUFReplay.Shared.Media.FfmpegInstallCoordinator.Status().Path);
    else
      throw new PlatformNotSupportedException("Camera recording is available on macOS and Windows.");
  }

  private void Cleanup(bool retentionChanged = false)
  {
    int removed = Store?.Cleanup(Main.Settings.WebcamRetentionDays) ?? 0;
    if (removed > 0 || retentionChanged)
      TUFReplay.Activity.Tracking.ActivityChanges.Notify();
  }

  private void Queue(string operation, Func<Task> action)
  {
    NotifyStateChanged();
    lock (_gate)
      _work = _work
        .ContinueWith(
          async _ =>
          {
            try
            {
              await action();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
              SetError(exception, operation);
            }
            finally
            {
              NotifyStateChanged();
            }
          },
          CancellationToken.None,
          TaskContinuationOptions.None,
          TaskScheduler.Default
        )
        .Unwrap();
  }

  private void SetError(Exception exception, string operation)
  {
    lock (_gate)
    {
      _error = exception.Message;
      // Finalizing one video does not invalidate a healthy shared camera.
      if (operation != "capture.end")
        _armed = false;
    }
    NotifyStateChanged();
    UnityMainThread.Post(() => Main.Instance?.LogException("Webcam", exception));
    if (Main.Settings?.WebcamEnabled == true)
      UnityMainThread.Post(() =>
        ReplayTimelineHud.ShowNotificationToast("Camera recording unavailable", exception.Message)
      );
  }

  private static void CopyWebcamSettings(TUFReplaySetting source, TUFReplaySetting destination)
  {
    destination.WebcamEnabled = source.WebcamEnabled;
    destination.WebcamDeviceId = source.WebcamDeviceId;
    destination.WebcamQuality = source.WebcamQuality;
    destination.WebcamOffsetMs = source.WebcamOffsetMs;
    destination.WebcamRecordingLimitMb = source.WebcamRecordingLimitMb;
    destination.WebcamRetentionDays = source.WebcamRetentionDays;
    destination.WebcamPlaybackVisible = source.WebcamPlaybackVisible;
    destination.WebcamLiveVisible = source.WebcamLiveVisible;
    destination.WebcamMirror = source.WebcamMirror;
    destination.WebcamFlipVertical = source.WebcamFlipVertical;
    destination.WebcamOverlayX = source.WebcamOverlayX;
    destination.WebcamOverlayY = source.WebcamOverlayY;
    destination.WebcamOverlayWidth = source.WebcamOverlayWidth;
    destination.WebcamOverlayLeft = source.WebcamOverlayLeft;
    destination.WebcamOverlayTop = source.WebcamOverlayTop;
    destination.WebcamCropX = source.WebcamCropX;
    destination.WebcamCropY = source.WebcamCropY;
    destination.WebcamCropWidth = source.WebcamCropWidth;
    destination.WebcamCropHeight = source.WebcamCropHeight;
  }
}
