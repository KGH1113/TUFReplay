using System;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using UnityEngine;

namespace TUFReplay.Microphone.Permissions;

internal static class MicrophonePermissionWarningCoordinator
{
  private static readonly MicrophonePermissionWarningPolicy Policy = new MicrophonePermissionWarningPolicy();
  private static bool _initialized;
  private static bool _pendingEditorLevel;
  private static bool _refreshRequested;

  internal static void Initialize()
  {
    Policy.Reset();
    _pendingEditorLevel = false;
    _refreshRequested = false;
    _initialized = true;
  }

  internal static void Shutdown()
  {
    _initialized = false;
    _pendingEditorLevel = false;
    _refreshRequested = false;
    Policy.Reset();
    ReplayTimelineHud.ResetMicrophonePermissionWarning();
  }

  internal static void NotifyEditorLevelLoaded()
  {
    if (!_initialized || Policy.HasShown || !IsEligibleRuntime())
      return;

    _pendingEditorLevel = true;
    _refreshRequested = false;
  }

  internal static void Tick()
  {
    if (!_initialized || !_pendingEditorLevel || Policy.HasShown)
      return;
    if (!IsEligibleRuntime())
    {
      ClearPending();
      return;
    }

    scnEditor editor = scnEditor.instance;
    if (
      editor == null
      || !string.Equals(ADOBase.sceneName, "scnEditor", StringComparison.Ordinal)
      || editor.playMode
      || !editor.initialized
      || editor.isLoading
    )
      return;

    if (!_refreshRequested)
    {
      FeatureRegistry.MicrophoneRecording?.RefreshPermissionStatus();
      _refreshRequested = true;
    }

    MicrophonePermissionStatus status =
      FeatureRegistry.MicrophoneRecording?.GetPermissionStatus()
      ?? new MicrophonePermissionStatus
      {
        State = MicrophonePermissionState.Failed,
        Error = "Microphone capture is unavailable.",
      };
    if (
      status.State == MicrophonePermissionState.Unknown
      || status.State == MicrophonePermissionState.Requesting
      || status.State == MicrophonePermissionState.Checking
    )
      return;

    if (!Policy.ShouldShow(status.State))
    {
      ClearPending();
      return;
    }

    if (!ReplayTimelineHud.ShowMicrophonePermissionWarning())
      return;

    Policy.MarkShown();
    ClearPending();
    Main.Instance?.Log(
      "[Microphone] Permission warning displayed after editor level load. state="
        + status.State
        + (string.IsNullOrEmpty(status.Error) ? string.Empty : ", error=" + status.Error)
    );
  }

  private static bool IsEligibleRuntime()
  {
    return Application.platform == RuntimePlatform.OSXPlayer
      && TUFReplaySettingStore.Current?.MicrophoneEnabled != false;
  }

  private static void ClearPending()
  {
    _pendingEditorLevel = false;
    _refreshRequested = false;
  }
}
