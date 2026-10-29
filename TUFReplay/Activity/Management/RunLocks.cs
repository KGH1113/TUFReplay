using System;
using TUFReplay.Activity.Repositories;
using TUFReplay.Composition;
using TUFReplay.Microphone.Repositories;

namespace TUFReplay.Activity.Management;

// A locked run can't be deleted, and its microphone and camera recordings never expire.
public static class RunLocks
{
  // Returns false when the run does not exist.
  public static bool Change(string runId, bool locked)
  {
    if (!RunRepository.SetLocked(runId, locked))
      return false;
    if (locked)
      KeepRecordings(runId);
    return true;
  }

  public static void KeepRecordings(string runId)
  {
    KeepMicrophoneRecording(runId);
    KeepCameraRecording(runId);
  }

  // A recording can finish saving after its run was locked; the save paths call these.
  public static void KeepMicrophoneRecordingIfLocked(string runId)
  {
    if (IsLocked(runId))
      KeepMicrophoneRecording(runId);
  }

  public static void KeepCameraRecordingIfLocked(string runId)
  {
    if (IsLocked(runId))
      KeepCameraRecording(runId);
  }

  private static bool IsLocked(string runId)
  {
    try
    {
      return RunRepository.IsLocked(runId);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[RunLock] Lock check failed. runId=" + runId + ", error=" + exception.Message);
      return false;
    }
  }

  private static void KeepMicrophoneRecording(string runId)
  {
    try
    {
      MicrophoneRecordingRepository.KeepPermanently(runId);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[RunLock] Microphone recording keep failed. runId=" + runId + ", error=" + exception.Message);
    }
  }

  private static void KeepCameraRecording(string runId)
  {
    try
    {
      FeatureRegistry.WebcamRecording?.Store?.KeepPermanently(runId);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[RunLock] Camera recording keep failed. runId=" + runId + ", error=" + exception.Message);
    }
  }
}
