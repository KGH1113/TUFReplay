using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Microphone;
using TUFReplay.Infrastructure.Settings;
using UnityEngine;

namespace TUFReplay.Features.Microphone;

public sealed class MicrophoneRecordingFeature
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

  public void Enable()
  {
    if (_active)
      return;
    _active = true;
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
      _backend.RequestPermission();
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
        Main.Instance?.Log("[Microphone] Capture finalization failed. error=" + task.Exception.GetBaseException().Message);
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
      if (TUFReplaySettingStore.Current?.AutoRecord != false)
        backend.RequestPermission();
      _backend = backend;
      _ticker = ticker;
      MicrophoneCaptureRuntime.Backend = backend;
    }
    catch
    {
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

  public void Persist(CapturedMicrophoneRecording recording)
  {
    if (recording == null)
      return;

    bool deleted;
    lock (_gate)
    {
      deleted = _deletedRuns.Contains(recording.RunId);
      if (!deleted)
        _persisting.Add(recording.RunId);
    }
    if (deleted)
    {
      Delete(recording.TempPath);
      return;
    }

    string pending = PendingPath(recording.RunId);
    try
    {
      if (File.Exists(pending))
        File.Delete(pending);
      File.Move(recording.TempPath, pending);
      recording.TempPath = pending;
      File.WriteAllText(MetadataPath(pending), JsonConvert.SerializeObject(recording));
      QueueSave(recording);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Failed to queue recording save. error=" + exception.Message);
      Delete(recording.TempPath);
      Delete(MetadataPath(pending));
    }
    finally
    {
      lock (_gate)
      {
        _persisting.Remove(recording.RunId);
        Monitor.PulseAll(_gate);
      }
    }
  }

  public void Discard(CapturedMicrophoneRecording recording)
  {
    if (recording == null)
      return;
    Delete(recording.TempPath);
  }

  public void NotifyRunPersisted(string runId)
  {
    lock (_gate)
    {
      if (_deletedRuns.Contains(runId))
        return;
      _persistedRuns.Add(runId);
    }
    string path = PendingPath(runId);
    if (File.Exists(path))
      QueueSave(ReadMetadata(path));
  }

  private void QueueSave(CapturedMicrophoneRecording recording)
  {
    if (recording == null || !File.Exists(recording.TempPath))
      return;
    lock (_gate)
    {
      if (_deletedRuns.Contains(recording.RunId) || !_persistedRuns.Contains(recording.RunId))
        return;
      if (!_saving.Add(recording.RunId))
        return;
      _savesIdle.Reset();
    }

    ThreadPool.QueueUserWorkItem(_ =>
    {
      try
      {
        MicrophoneRecordingRepository.Save(recording);
        Delete(recording.TempPath);
        Delete(MetadataPath(recording.TempPath));
        Main.Instance?.Log("[Microphone] Recording saved. runId=" + recording.RunId);
      }
      catch (Exception exception)
      {
        Main.Instance?.Log(
          "[Microphone] Recording save deferred. runId=" + recording.RunId + ", error=" + exception.Message
        );
      }
      finally
      {
        lock (_gate)
        {
          _saving.Remove(recording.RunId);
          Monitor.PulseAll(_gate);
          if (_saving.Count == 0)
            _savesIdle.Set();
        }
      }
    });
  }

  public void BeginRunDeletion(string runId)
  {
    lock (_gate)
    {
      _deletedRuns.Add(runId);
      while (_persisting.Contains(runId) || _saving.Contains(runId))
        Monitor.Wait(_gate);
    }
  }

  public void CompleteRunDeletion(string runId)
  {
    lock (_gate)
      _persistedRuns.Remove(runId);
    string pending = PendingPath(runId);
    Delete(pending);
    Delete(MetadataPath(pending));
  }

  public void CancelRunDeletion(string runId)
  {
    lock (_gate)
      _deletedRuns.Remove(runId);
  }

  private void RecoverPendingSaves()
  {
    foreach (string metadata in Directory.GetFiles(_tempDirectory, "*.wav.save-pending.json"))
    {
      string wav = metadata.Substring(0, metadata.Length - ".json".Length);
      if (!File.Exists(wav))
        Delete(metadata);
    }
    foreach (string path in Directory.GetFiles(_tempDirectory, "*.wav.save-pending"))
    {
      CapturedMicrophoneRecording recording = ReadMetadata(path);
      if (recording == null || !MicrophoneRecordingRepository.RunExists(recording.RunId))
      {
        Delete(path);
        Delete(MetadataPath(path));
        continue;
      }
      lock (_gate)
        _persistedRuns.Add(recording.RunId);
      QueueSave(recording);
    }
  }

  private CapturedMicrophoneRecording ReadMetadata(string path)
  {
    try
    {
      string metadataPath = MetadataPath(path);
      if (!File.Exists(metadataPath))
        return null;
      CapturedMicrophoneRecording recording = JsonConvert.DeserializeObject<CapturedMicrophoneRecording>(
        File.ReadAllText(metadataPath)
      );
      if (recording != null)
        recording.TempPath = path;
      return recording;
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Pending recording metadata is invalid. error=" + exception.Message);
      return null;
    }
  }

  private void DeleteStalePartials()
  {
    foreach (string path in Directory.GetFiles(_tempDirectory, "*.partial"))
      Delete(path);
  }

  private void DeleteExpiredRecordings()
  {
    if (Interlocked.Exchange(ref _retentionCleanupRunning, 1) != 0)
      return;
    try
    {
      int deleted = MicrophoneRecordingRepository.DeleteExpired(DateTime.UtcNow);
      if (deleted > 0)
        Main.Instance?.Log("[Microphone] Deleted expired temporary recordings. count=" + deleted);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Expired recording cleanup failed. error=" + exception.Message);
    }
    finally
    {
      Interlocked.Exchange(ref _retentionCleanupRunning, 0);
    }
  }

  private string PendingPath(string runId) => Path.Combine(_tempDirectory, runId + ".wav.save-pending");

  private static string MetadataPath(string path) => path + ".json";

  private static void Delete(string path)
  {
    try
    {
      if (!string.IsNullOrEmpty(path) && File.Exists(path))
        File.Delete(path);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Temp file cleanup failed. error=" + exception.Message);
    }
  }
}

public sealed class MicrophoneCaptureTicker : MonoBehaviour
{
  public IMicrophoneCaptureBackend Backend;

  private void Update() => Backend?.Tick();
}
