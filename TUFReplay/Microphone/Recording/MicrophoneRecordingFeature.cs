using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Microphone.Capture;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Recording;
using TUFReplay.Shared.Settings;
using UnityEngine;

namespace TUFReplay.Microphone.Recording;

public sealed partial class MicrophoneRecordingFeature
{
  private readonly object _gate = new object();
  private readonly HashSet<string> _persisting = new HashSet<string>(StringComparer.Ordinal);
  private readonly HashSet<string> _saving = new HashSet<string>(StringComparer.Ordinal);
  private readonly HashSet<string> _persistedRuns = new HashSet<string>(StringComparer.Ordinal);
  private readonly HashSet<string> _deletedRuns = new HashSet<string>(StringComparer.Ordinal);
  private readonly ManualResetEventSlim _savesIdle = new ManualResetEventSlim(true);
  private readonly ManualResetEventSlim _finalizationsIdle = new ManualResetEventSlim(true);
  private int _finalizationCount;
  private IMicrophoneCaptureBackend _backend;
  private MicrophoneCaptureTicker _ticker;
  private System.Threading.Timer _retentionTimer;
  private int _retentionCleanupRunning;
  private string _tempDirectory;
  private bool _active;
  private bool _permissionRequestStarted;

  public void Enable()
  {
    if (_active)
      return;
    _active = true;
    _permissionRequestStarted = false;
    try
    {
      _tempDirectory = Path.Combine(Main.Instance.InstallPath, "Data", "MicrophoneTemp");
      Directory.CreateDirectory(_tempDirectory);
      DeleteStalePartials();
      try
      {
        RecoverPendingSaves();
      }
      catch (Exception exception)
      {
        Main.Instance?.LogException("Microphone/Recovery", exception);
      }
      _retentionTimer = new System.Threading.Timer(
        _ => DeleteExpiredRecordings(),
        null,
        TimeSpan.Zero,
        TimeSpan.FromHours(1)
      );
      if (TUFReplaySettingStore.Current?.MicrophoneEnabled != false)
      {
        try
        {
          ActivateCaptureBackend();
        }
        catch (Exception exception)
        {
          DeactivateCaptureBackend();
          Main.Instance?.LogException("Microphone/CaptureInitialize", exception);
        }
      }
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Microphone/Initialize", exception);
      _active = false;
      _retentionTimer?.Dispose();
      _retentionTimer = null;
      DeactivateCaptureBackend();
    }
  }

  public void Disable()
  {
    if (!_active)
      return;
    _active = false;
    _retentionTimer?.Dispose();
    _retentionTimer = null;
    _finalizationsIdle.Wait(TimeSpan.FromSeconds(2));
    DeactivateCaptureBackend();
    _savesIdle.Wait(TimeSpan.FromSeconds(2));
    lock (_gate)
    {
      _persistedRuns.Clear();
      _persisting.Clear();
      _deletedRuns.Clear();
    }
  }

  public bool SetCaptureEnabled(bool enabled, out string error)
  {
    error = null;
    if (!_active)
    {
      error = "Microphone capture is unavailable.";
      return false;
    }

    if (enabled)
    {
      try
      {
        ActivateCaptureBackend();
        return true;
      }
      catch (Exception exception)
      {
        DeactivateCaptureBackend();
        error = exception.Message;
        return false;
      }
    }

    DeactivateCaptureBackend();
    return true;
  }

  public void ArmForLevel()
  {
    if (!IsCaptureEnabled() || _backend == null)
      return;
    try
    {
      string deviceId = TUFReplaySettingStore.Current.MicrophoneDeviceId;
      if (!_backend.Arm(deviceId, out string error))
        Main.Instance?.Log("[Microphone] Arm failed; this run will have no recording. error=" + error);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Arm failed; this run will have no recording. error=" + exception.Message);
    }
  }

  public bool ArmForCalibration(out string error)
  {
    error = null;
    if (!IsCaptureEnabled())
    {
      error = "Microphone input is turned off.";
      return false;
    }
    if (_backend == null)
    {
      error = "Microphone capture is unavailable.";
      return false;
    }

    try
    {
      RequestPermissionOnce();
      return _backend.Arm(TUFReplaySettingStore.Current.MicrophoneDeviceId, out error);
    }
    catch (Exception exception)
    {
      error = exception.Message;
      return false;
    }
  }

  public MicrophoneArmStatus GetArmStatus() =>
    _backend?.GetArmStatus()
    ?? new MicrophoneArmStatus { State = MicrophoneArmState.Failed, Error = "Microphone capture is unavailable." };

  public MicrophonePermissionStatus GetPermissionStatus() =>
    _backend?.GetPermissionStatus()
    ?? new MicrophonePermissionStatus
    {
      State = IsCaptureEnabled() ? MicrophonePermissionState.Failed : MicrophonePermissionState.NotApplicable,
      Error = IsCaptureEnabled() ? "Microphone capture is unavailable." : null,
    };

  public void RefreshPermissionStatus()
  {
    if (!IsCaptureEnabled())
      return;
    _backend?.RefreshPermissionStatus();
  }

  public void Disarm()
  {
    try
    {
      _backend?.Disarm();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Disarm failed. error=" + exception.Message);
    }
  }

  public void BeginRun(string runId)
  {
    if (!IsCaptureEnabled() || _backend == null || string.IsNullOrEmpty(runId))
      return;
    string path = Path.Combine(_tempDirectory, runId + ".wav.partial");
    try
    {
      if (!_backend.BeginRun(runId, path, out string error))
        Main.Instance?.Log("[Microphone] Capture start failed; this run will have no recording. error=" + error);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log(
        "[Microphone] Capture start failed; this run will have no recording. error=" + exception.Message
      );
    }
  }

  public void EndRun(Action<CapturedMicrophoneRecording> completed)
  {
    Task<CapturedMicrophoneRecording> finalization;
    try
    {
      finalization = _backend?.EndRunAsync() ?? Task.FromResult<CapturedMicrophoneRecording>(null);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Capture finalization failed. error=" + exception.Message);
      finalization = Task.FromResult<CapturedMicrophoneRecording>(null);
    }

    lock (_gate)
    {
      _finalizationCount++;
      _finalizationsIdle.Reset();
    }
    finalization.ContinueWith(
      task => CompleteFinalization(task, completed),
      CancellationToken.None,
      TaskContinuationOptions.None,
      TaskScheduler.Default
    );
  }

  private void CompleteFinalization(
    Task<CapturedMicrophoneRecording> task,
    Action<CapturedMicrophoneRecording> completed
  )
  {
    CapturedMicrophoneRecording recording = null;
    try
    {
      if (task.Status == TaskStatus.RanToCompletion)
        recording = task.Result;
      else if (task.Exception != null)
        Main.Instance?.Log(
          "[Microphone] Capture finalization failed. error=" + task.Exception.GetBaseException().Message
        );
      completed?.Invoke(recording);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Capture completion failed. error=" + exception.Message);
      Discard(recording);
    }
    finally
    {
      lock (_gate)
      {
        _finalizationCount--;
        if (_finalizationCount == 0)
          _finalizationsIdle.Set();
      }
    }
  }

  private static bool IsCaptureEnabled()
  {
    return TUFReplaySettingStore.Current?.MicrophoneEnabled != false;
  }

  private void ActivateCaptureBackend()
  {
    if (_backend != null)
      return;

    IMicrophoneCaptureBackend backend = MicrophoneCaptureBackendFactory.Create();
    GameObject gameObject = null;
    try
    {
      gameObject = new GameObject("TUFReplay Microphone Capture Ticker");
      UnityEngine.Object.DontDestroyOnLoad(gameObject);
      MicrophoneCaptureTicker ticker = gameObject.AddComponent<MicrophoneCaptureTicker>();
      ticker.Backend = backend;
      _backend = backend;
      _ticker = ticker;
      MicrophoneCaptureRuntime.Backend = backend;
      RequestPermissionOnce();
    }
    catch
    {
      _backend = null;
      _ticker = null;
      MicrophoneCaptureRuntime.Backend = null;
      if (gameObject != null)
        UnityEngine.Object.Destroy(gameObject);
      backend.Dispose();
      throw;
    }
  }

  private void DeactivateCaptureBackend()
  {
    IMicrophoneCaptureBackend backend = _backend;
    _backend = null;
    MicrophoneCaptureRuntime.Backend = null;

    if (_ticker != null)
      UnityEngine.Object.Destroy(_ticker.gameObject);
    _ticker = null;

    if (backend == null)
      return;
    try
    {
      backend.Disarm();
      bool finalizing;
      lock (_gate)
        finalizing = _finalizationCount > 0;
      if (finalizing)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          _finalizationsIdle.Wait();
          DisposeBackend(backend);
        });
      }
      else
        backend.Dispose();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Shutdown failed. error=" + exception.Message);
    }
  }

  private void RequestPermissionOnce()
  {
    if (_permissionRequestStarted || _backend == null)
      return;
    _permissionRequestStarted = true;
    _backend.RequestPermission();
  }

  private static void DisposeBackend(IMicrophoneCaptureBackend backend)
  {
    try
    {
      backend.Dispose();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Deferred shutdown failed. error=" + exception.Message);
    }
  }
}

public sealed class MicrophoneCaptureTicker : MonoBehaviour
{
  public IMicrophoneCaptureBackend Backend;

  private void Update() => Backend?.Tick();
}
