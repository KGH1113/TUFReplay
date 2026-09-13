using System;
using TUFReplay.Composition;
using TUFReplay.Microphone.Models;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Settings;
using UnityEngine;

namespace TUFReplay.Microphone.Permissions;

internal static class MicrophonePermissionWarningCoordinator
{
  private const string MicrophoneSettingsUrl =
    "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone";
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
    ReplayTimelineHud.ResetNotification();
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

    WarningCopy warning = WarningFor(status.State);
    if (
      !ReplayTimelineHud.ShowPersistentNotification(
        warning.Title,
        warning.Message,
        warning.ActionLabel,
        warning.ActionLabel == null ? null : OpenMicrophoneSettings
      )
    )
      return;

    Policy.MarkShown();
    ClearPending();
    Main.Instance?.Log(
      "[Microphone] Permission warning displayed after editor level load. state="
        + status.State
        + (string.IsNullOrEmpty(status.Error) ? string.Empty : ", error=" + status.Error)
    );
  }

  private static WarningCopy WarningFor(MicrophonePermissionState state)
  {
    if (state == MicrophonePermissionState.Denied)
    {
      return new WarningCopy(
        "Microphone access is off",
        "Allow TUFReplay Microphone Capture in System Settings → Privacy & Security → Microphone. "
          + "This run will continue without microphone audio.",
        "Open System Settings"
      );
    }

    if (state == MicrophonePermissionState.Restricted)
    {
      return new WarningCopy(
        "Microphone access is restricted",
        "Screen Time or a device management policy is blocking microphone access. Check the restriction or contact "
          + "your administrator. This run will continue without microphone audio."
      );
    }

    return new WarningCopy(
      "Microphone recording could not start",
      "Restart the game. If this keeps happening, reinstall TUFReplay and check the mod log. "
        + "This run will continue without microphone audio."
    );
  }

  private static void OpenMicrophoneSettings() => Application.OpenURL(MicrophoneSettingsUrl);

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

  private sealed class WarningCopy
  {
    public readonly string Title;
    public readonly string Message;
    public readonly string ActionLabel;

    public WarningCopy(string title, string message, string actionLabel = null)
    {
      Title = title;
      Message = message;
      ActionLabel = actionLabel;
    }
  }
}
