using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Repositories;
using UnityEngine;

namespace TUFReplay.Microphone.Recording;

public sealed partial class MicrophoneRecordingFeature
{
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
    try
    {
      MicrophoneRecordingRepository.Delete(runId);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Orphan cleanup deferred. runId=" + runId + ", error=" + exception.Message);
    }
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
