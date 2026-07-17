using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;
using TUFReplay.Features.Ui;
using TUFReplay.Infrastructure.Database.Repositories;
using TUFReplay.Infrastructure.Microphone;
using TUFReplay.Infrastructure.Settings;
using UnityEngine;

namespace TUFReplay.Features.Microphone;

public sealed class MicrophoneRecordingFeature
{
  private readonly object _gate = new object();
  private readonly HashSet<string> _saving = new HashSet<string>(StringComparer.Ordinal);
  private readonly HashSet<string> _persistedRuns = new HashSet<string>(StringComparer.Ordinal);
  private readonly ManualResetEventSlim _savesIdle = new ManualResetEventSlim(true);
  private IMicrophoneCaptureBackend _backend;
  private MicrophoneCaptureTicker _ticker;
  private CapturedMicrophoneRecording _awaitingDecision;
  private string _tempDirectory;

  public void Enable()
  {
    if (_backend != null)
      return;
    try
    {
      _tempDirectory = Path.Combine(Main.Instance.Path, "Data", "MicrophoneTemp");
      Directory.CreateDirectory(_tempDirectory);
      DeleteStalePartials();
      _backend = MicrophoneCaptureBackendFactory.Create();
      MicrophoneCaptureRuntime.Backend = _backend;
      if (TUFReplaySettingStore.Current?.AutoRecord != false)
        _backend.RequestPermission();
      var gameObject = new GameObject("TUFReplay Microphone Capture Ticker");
      UnityEngine.Object.DontDestroyOnLoad(gameObject);
      _ticker = gameObject.AddComponent<MicrophoneCaptureTicker>();
      _ticker.Backend = _backend;
      try
      {
        RecoverPendingSaves();
      }
      catch (Exception exception)
      {
        Main.Instance?.LogException("Microphone/Recovery", exception);
      }
    }
    catch (Exception exception)
    {
      Main.Instance?.LogException("Microphone/Initialize", exception);
      _backend?.Dispose();
      _backend = null;
      MicrophoneCaptureRuntime.Backend = null;
      if (_ticker != null)
        UnityEngine.Object.Destroy(_ticker.gameObject);
      _ticker = null;
    }
  }

  public void Disable()
  {
    DiscardAwaitingDecision();
    Disarm();
    try
    {
      _backend?.Dispose();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Shutdown failed. error=" + exception.Message);
    }
    _backend = null;
    MicrophoneCaptureRuntime.Backend = null;
    _savesIdle.Wait(TimeSpan.FromSeconds(2));
    lock (_gate)
      _persistedRuns.Clear();
    if (_ticker != null)
      UnityEngine.Object.Destroy(_ticker.gameObject);
    _ticker = null;
  }

  public void ArmForLevel()
  {
    if (_backend == null)
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
    if (_backend == null || string.IsNullOrEmpty(runId))
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

  public CapturedMicrophoneRecording EndRun()
  {
    try
    {
      return _backend?.EndRun();
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Capture finalization failed. error=" + exception.Message);
      return null;
    }
  }

  public void Present(CapturedMicrophoneRecording recording)
  {
    if (recording == null)
      return;
    DiscardAwaitingDecision();
    _awaitingDecision = recording;
    if (
      Bootstrap.FeatureRegistry.MicrophoneRecordingToast == null
      || !Bootstrap.FeatureRegistry.MicrophoneRecordingToast.Show(result => Resolve(recording, result))
    )
    {
      _awaitingDecision = null;
      Delete(recording.TempPath);
    }
  }

  public void Discard(CapturedMicrophoneRecording recording)
  {
    if (recording == null)
      return;
    if (ReferenceEquals(_awaitingDecision, recording))
      _awaitingDecision = null;
    Delete(recording.TempPath);
  }

  public void NotifyRunPersisted(string runId)
  {
    lock (_gate)
      _persistedRuns.Add(runId);
    string path = PendingPath(runId);
    if (File.Exists(path))
      QueueSave(ReadMetadata(path));
  }

  private void Resolve(CapturedMicrophoneRecording recording, MicrophoneRecordingToastResult result)
  {
    if (!ReferenceEquals(_awaitingDecision, recording))
      return;
    _awaitingDecision = null;
    if (result.Decision == MicrophoneRecordingToastDecision.Discard)
    {
      Delete(recording.TempPath);
      Main.Instance?.Log(
        result.Reason == MicrophoneRecordingToastReason.Timeout
          ? "[Microphone] Recording discarded by timeout."
          : "[Microphone] Recording discarded."
      );
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
  }

  private void QueueSave(CapturedMicrophoneRecording recording)
  {
    if (recording == null || !File.Exists(recording.TempPath))
      return;
    lock (_gate)
    {
      if (!_persistedRuns.Contains(recording.RunId))
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
          if (_saving.Count == 0)
            _savesIdle.Set();
        }
      }
    });
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

  private void DiscardAwaitingDecision()
  {
    CapturedMicrophoneRecording previous = _awaitingDecision;
    _awaitingDecision = null;
    if (previous != null)
      Delete(previous.TempPath);
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
