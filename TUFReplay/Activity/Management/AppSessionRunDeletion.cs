using System;
using System.Collections.Generic;
using TUFReplay.Activity.Repositories;
using TUFReplay.Composition;
using TUFReplay.Microphone.Recording;
using TUFReplay.Webcam.Repositories;

namespace TUFReplay.Activity.Management;

public readonly struct RunBatchDeletion
{
  public RunBatchDeletion(int deletedRunCount, int keptLockedRunCount, int keptInUseRunCount, int newlyLockedRunCount)
  {
    DeletedRunCount = deletedRunCount;
    KeptLockedRunCount = keptLockedRunCount;
    KeptInUseRunCount = keptInUseRunCount;
    NewlyLockedRunCount = newlyLockedRunCount;
  }

  public int DeletedRunCount { get; }
  public int KeptLockedRunCount { get; }
  public int KeptInUseRunCount { get; }
  public int NewlyLockedRunCount { get; }
}

// Deletes every unlocked run recorded in a set of app sessions (the web groups them into a day).
// Locked runs and the run playing as a replay (runInUseId) stay. Callers run this while gameplay is idle.
public static class AppSessionRunDeletion
{
  public static RunBatchDeletion Delete(IReadOnlyList<string> appSessionIds, bool keepClears, string runInUseId)
  {
    int newlyLocked = 0;
    if (keepClears)
    {
      List<string> lockedClears = RunRepository.LockClearsInAppSessions(appSessionIds);
      foreach (string runId in lockedClears)
        RunLocks.KeepRecordings(runId);
      newlyLocked = lockedClears.Count;
    }

    List<string> candidates = RunRepository.ListUnlockedRunIdsInAppSessions(appSessionIds);
    int keptInUse = !string.IsNullOrEmpty(runInUseId) && candidates.Remove(runInUseId) ? 1 : 0;

    MicrophoneRecordingFeature microphone = FeatureRegistry.MicrophoneRecording;
    foreach (string runId in candidates)
      microphone?.BeginRunDeletion(runId);

    List<string> deleted;
    try
    {
      deleted = RunRepository.DeleteUnlocked(candidates);
    }
    catch
    {
      foreach (string runId in candidates)
        microphone?.CancelRunDeletion(runId);
      throw;
    }

    // A run locked after it was listed is skipped by the delete; release its recordings.
    var deletedIds = new HashSet<string>(deleted, StringComparer.Ordinal);
    foreach (string runId in candidates)
      if (!deletedIds.Contains(runId))
        microphone?.CancelRunDeletion(runId);

    microphone?.CompleteRunDeletions(deleted);
    WebcamRecordingStore cameraStore = FeatureRegistry.WebcamRecording?.Store;
    if (cameraStore != null)
      foreach (string runId in deleted)
        cameraStore.DeleteRun(runId);

    return new RunBatchDeletion(
      deleted.Count,
      RunRepository.CountLockedRunsInAppSessions(appSessionIds),
      keptInUse,
      newlyLocked
    );
  }
}
