using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Capture;
using TUFReplay.Shared.Settings;
using TUFReplay.Shared.Timing;
using TUFReplay.Shared.Unity;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Ipc;
using TUFReplay.Webcam.Models;
using TUFReplay.Webcam.Repositories;
using TUFReplay.Webcam.Timing;

namespace TUFReplay.Webcam.Recording;

public sealed class WebcamRecordingFeature
{
  private readonly object _gate = new object();
  private Task _work = Task.CompletedTask;
  private IWebcamCaptureBackend _backend;
  private List<WebcamDevice> _devices = new List<WebcamDevice>();
  private bool _active;
  private bool _armed;
  private bool _arming;
  private bool _recording;
  private bool _refreshQueued;
  private DateTime _lastDeviceRefreshUtc = DateTime.MinValue;
  private int _finalizing;
  private int _generation;
  private long _operationId;
  private string _runId;
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
    try
    {
      Store = new WebcamRecordingStore(Path.Combine(Main.Instance.InstallPath, "Data", "Webcam"));
      CaptureDiagnostics.Record(
        "feature.enable",
        new
        {
          os = RuntimeInformation.OSDescription,
          architecture = RuntimeInformation.ProcessArchitecture.ToString(),
          modAssemblyId = typeof(WebcamRecordingFeature).Assembly.ManifestModule.ModuleVersionId,
          payloadPath = Main.Instance.PayloadPath,
          state = DiagnosticState(),
        }
      );
      Queue(
        "storage.initialize",
        () =>
        {
          Store.CleanupInterruptedCaptures();
          Cleanup();
          return Task.CompletedTask;
        }
      );
      _retention = new System.Threading.Timer(
        _ =>
          Queue(
            "storage.retention",
            () =>
            {
              Cleanup();
              return Task.CompletedTask;
            }
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
    lock (_gate)
    {
      _active = false;
      _armed = false;
      _arming = false;
      _generation++;
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
        StorageLimitMb = settings.WebcamStorageLimitMb,
        RetentionDays = settings.WebcamRetentionDays,
        PlaybackVisible = settings.WebcamPlaybackVisible,
        LiveVisible = settings.WebcamLiveVisible,
        Mirror = settings.WebcamMirror,
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
          Cleanup();
          return Task.CompletedTask;
        }
      );
    return true;
  }

  public void ArmForLevel()
  {
    if (!Supported || !_active || !Main.Settings.WebcamEnabled)
      return;
    string device = Main.Settings.WebcamDeviceId;
    WebcamCaptureProfile profile = WebcamCaptureProfile.ForQuality(Main.Settings.WebcamQuality);
    int generation;
    lock (_gate)
    {
      if (_recording || _armed || _arming)
        return;
      generation = ++_generation;
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
            await TUFReplay.Shared.Media.FfmpegInstallCoordinator.EnsureAvailableAsync(() =>
              _active && generation == _generation
            );
          if (!_active || generation != _generation)
            return;
          EnsureBackend();
          CaptureDiagnostics.Record(
            "capture.arm.request",
            new
            {
              generation,
              deviceId = device,
              profile.Width,
              profile.Height,
              profile.FrameRate,
              profile.BitRate,
            }
          );
          await _backend.ArmAsync(device, profile, Store.CaptureDirectory);
          var frameWait = Stopwatch.StartNew();
          DateTime deadline = DateTime.UtcNow.AddSeconds(10);
          while (
            _backend.Preview?.HasFrame != true && DateTime.UtcNow < deadline && generation == _generation && _active
          )
            await Task.Delay(25);
          if (_backend.Preview?.HasFrame != true && generation == _generation && _active)
          {
            CaptureDiagnostics.Record(
              "capture.first-frame.timeout",
              new
              {
                generation,
                waitedMs = frameWait.Elapsed.TotalMilliseconds,
                preview = _backend.Preview?.DiagnosticState(),
              }
            );
            if (_backend is MacOsWebcamCaptureBackend mac)
              mac.LogDiagnosticSnapshot("first-frame.timeout");
            throw new IOException("The camera is not sending video. Reconnect it or choose another camera.");
          }
          lock (_gate)
            if (_active && generation == _generation)
              _armed = true;
          CaptureDiagnostics.Record(
            "capture.first-frame.result",
            new
            {
              generation,
              currentGeneration = _generation,
              waitedMs = frameWait.Elapsed.TotalMilliseconds,
              cancelled = !_active || generation != _generation,
              preview = _backend.Preview?.DiagnosticState(),
            }
          );
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

  public bool BeginRun(string runId)
  {
    IWebcamCaptureBackend backend;
    int generation;
    lock (_gate)
    {
      if (!_active || !Main.Settings.WebcamEnabled || !_armed || _backend == null || _recording)
      {
        if (Main.Settings.WebcamEnabled)
        {
          object skippedState = DiagnosticState();
          Queue(
            "capture.begin.skipped",
            () =>
            {
              CaptureDiagnostics.Record("capture.begin.skipped", new { runId, state = skippedState });
              return Task.CompletedTask;
            },
            runId
          );
        }
        return false;
      }
      _recording = true;
      _runId = runId;
      backend = _runBackend = _backend;
      generation = _runGeneration = _generation;
    }
    long budget = Main.Settings.WebcamStorageLimitMb * 1024L * 1024;
    int retention = Main.Settings.WebcamRetentionDays;
    Queue(
      "capture.begin",
      async () =>
      {
        // Readiness can change while this operation waits behind camera work.
        // Keep this run bound to the backend that was ready at its start.
        lock (_gate)
          if (!_active || !_armed || generation != _generation || !ReferenceEquals(backend, _backend))
            return;
        long maxBytes = Store.ReserveCaptureBytes(budget, retention);
        CaptureDiagnostics.Record(
          "capture.storage.reserved",
          new
          {
            runId,
            budgetBytes = budget,
            retentionDays = retention,
            maxBytes,
          }
        );
        if (maxBytes < 2 * 1024 * 1024)
          throw new IOException(
            "Camera storage is full. Increase the storage limit or stop the replay using older footage."
          );
        await backend.BeginAsync(runId, Store.TemporaryPath(runId), maxBytes);
      },
      runId
    );
    return true;
  }

  public void EndRun(WebcamCaptureTimeline timeline, Action<WebcamRecording> completed)
  {
    string runId;
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
      runId = _runId;
      _runId = null;
      backend = _runBackend;
      generation = _runGeneration;
      _runBackend = null;
    }
    Queue(
      "capture.end",
      async () =>
      {
        WebcamRecording recording = null;
        try
        {
          bool current;
          lock (_gate)
            current = backend != null && generation == _generation && ReferenceEquals(backend, _backend);
          if (current)
            recording = await backend.EndAsync();
          if (recording != null)
          {
            CaptureDiagnostics.Record(
              "capture.timeline.apply",
              new
              {
                runId,
                recording.CaptureStartTimestampTicks,
                timeline = timeline?.DiagnosticState(),
              }
            );
            if (timeline?.TryApplyTo(recording) != true)
            {
              // A run can end before its game clock advances. The camera is
              // still healthy; discard this video without disarming capture.
              CaptureDiagnostics.Record(
                "capture.recording.discarded",
                new
                {
                  runId,
                  reason = timeline?.First.HasValue != true ? "no_gameplay_clock_anchor" : "invalid_capture_timestamp",
                  recording.CaptureStartTimestampTicks,
                  timeline = timeline?.DiagnosticState(),
                  state = DiagnosticState(),
                }
              );
              WebcamRecordingStore.Discard(recording);
              recording = null;
            }
            else
              CaptureDiagnostics.Record(
                "capture.recording.complete",
                new
                {
                  runId,
                  recording.FilePath,
                  recording.DeviceId,
                  recording.Width,
                  recording.Height,
                  recording.FrameRate,
                  recording.DurationUs,
                  recording.CaptureStartTimestampTicks,
                  recording.CaptureStartOffsetUs,
                  recording.SizeLimited,
                }
              );
          }
          completed?.Invoke(recording);
        }
        catch (Exception exception)
        {
          WebcamRecordingStore.Discard(recording);
          SetError(exception, "capture.end", runId: runId);
          completed?.Invoke(null);
        }
        finally
        {
          lock (_gate)
            _finalizing--;
        }
      },
      runId
    );
  }

  public void Persist(WebcamRecording recording)
  {
    if (recording == null)
      return;
    long budget = Main.Settings.WebcamStorageLimitMb * 1024L * 1024;
    int retention = Main.Settings.WebcamRetentionDays;
    Queue(
      "storage.persist",
      () =>
      {
        try
        {
          bool saved = Store.Save(recording, budget, retention);
          CaptureDiagnostics.Record(
            "capture.storage.persisted",
            new
            {
              recording.RunId,
              saved,
              recording.SizeLimited,
              budgetBytes = budget,
              retentionDays = retention,
            }
          );
          if (!saved || recording.SizeLimited)
            UnityMainThread.Post(() =>
              ReplayTimelineHud.ShowNotificationToast(
                "Camera storage limit",
                saved
                  ? "The saved video ends at the storage limit. Increase the camera storage limit for longer recordings."
                  : "This video could not fit in camera storage. Increase the storage limit for the next run."
              )
            );
        }
        catch
        {
          WebcamRecordingStore.Discard(recording);
          throw;
        }
        return Task.CompletedTask;
      },
      recording.RunId
    );
  }

  public void Disarm()
  {
    lock (_gate)
    {
      _armed = false;
      _arming = false;
      _generation++;
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
          CaptureDiagnostics.Record(
            "capture.devices",
            new { selectedDeviceId = Main.Settings.WebcamDeviceId, devices }
          );
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
    CaptureDiagnostics.Record("capture.backend.created", new { backend = _backend.GetType().FullName });
  }

  private void Cleanup()
  {
    Store?.Cleanup(Main.Settings.WebcamStorageLimitMb * 1024L * 1024, Main.Settings.WebcamRetentionDays);
  }

  private void Queue(string operation, Func<Task> action, string runId = null)
  {
    long operationId = Interlocked.Increment(ref _operationId);
    long queuedAt = Stopwatch.GetTimestamp();
    lock (_gate)
      _work = _work
        .ContinueWith(
          async _ =>
          {
            var elapsed = Stopwatch.StartNew();
            CaptureDiagnostics.Record(
              "operation.begin",
              new
              {
                operation,
                operationId,
                runId,
                queueWaitMs = (Stopwatch.GetTimestamp() - queuedAt) * 1000d / Stopwatch.Frequency,
                state = DiagnosticState(),
              }
            );
            try
            {
              await action();
              CaptureDiagnostics.Record(
                "operation.complete",
                new
                {
                  operation,
                  operationId,
                  runId,
                  elapsedMs = elapsed.Elapsed.TotalMilliseconds,
                  state = DiagnosticState(),
                }
              );
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
              SetError(exception, operation, operationId, runId);
            }
          },
          CancellationToken.None,
          TaskContinuationOptions.None,
          TaskScheduler.Default
        )
        .Unwrap();
  }

  private object DiagnosticState()
  {
    lock (_gate)
      return new
      {
        active = _active,
        armed = _armed,
        arming = _arming,
        recording = _recording,
        finalizing = _finalizing,
        generation = _generation,
        runId = _runId,
        error = _error,
        backend = _backend?.GetType().Name,
        enabled = Main.Settings?.WebcamEnabled,
        selectedDeviceId = Main.Settings?.WebcamDeviceId,
        quality = Main.Settings?.WebcamQuality,
        storageLimitMb = Main.Settings?.WebcamStorageLimitMb,
        retentionDays = Main.Settings?.WebcamRetentionDays,
      };
  }

  private void SetError(Exception exception, string operation, long? operationId = null, string runId = null)
  {
    CaptureDiagnostics.Record(
      "operation.failed",
      new
      {
        operation,
        operationId,
        runId,
        state = DiagnosticState(),
        preview = _backend?.Preview?.DiagnosticState(),
      },
      exception
    );
    lock (_gate)
    {
      _error = exception.Message;
      _armed = false;
    }
    Main.Instance?.LogException("Webcam", exception);
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
    destination.WebcamStorageLimitMb = source.WebcamStorageLimitMb;
    destination.WebcamRetentionDays = source.WebcamRetentionDays;
    destination.WebcamPlaybackVisible = source.WebcamPlaybackVisible;
    destination.WebcamLiveVisible = source.WebcamLiveVisible;
    destination.WebcamMirror = source.WebcamMirror;
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
