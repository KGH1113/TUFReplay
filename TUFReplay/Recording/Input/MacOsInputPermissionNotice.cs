using TUFReplay.Replay.Timeline;
using UnityEngine;

namespace TUFReplay.Recording.Input;

internal static class MacOsInputPermissionNotice
{
  private const string SettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent";

  public static void Show(string reason)
  {
    string title;
    string message;
    string actionLabel = null;
    System.Action action = null;

    if (IsMissingNativeComponent(reason))
    {
      title = "Native input is unavailable";
      message = "The macOS native input component is missing or invalid. Reinstall TUFReplay.";
    }
    else if (IsPermissionFailure(reason))
    {
      title = "Input Monitoring is required";
      message =
        "Allow A Dance of Fire and Ice in System Settings → Privacy & Security → Input Monitoring, "
        + "then restart the game.";
      actionLabel = "Open System Settings";
      action = OpenSystemSettings;
    }
    else if (IsResourceBusy(reason))
    {
      title = "Native input is busy";
      message = "Another app or input utility may be holding exclusive access. Close it, then restart the game.";
    }
    else
    {
      title = "Native input failed to start";
      message = "TUFReplay could not start macOS native keyboard capture. Restart the game and check the log if it repeats.";
    }

    message += "\nReason: " + (string.IsNullOrWhiteSpace(reason) ? "unknown" : reason);

    if (!ReplayTimelineHud.ShowPersistentNotification(title, message, actionLabel, action))
      Main.Instance?.Log("[Recording/Input] Runtime notification UI is unavailable. reason=" + reason);
  }

  private static bool IsMissingNativeComponent(string reason) =>
    reason?.StartsWith("dylib_missing_or_invalid", System.StringComparison.Ordinal) == true;

  private static bool IsPermissionFailure(string reason) =>
    reason?.StartsWith("input_monitoring_", System.StringComparison.Ordinal) == true
    || reason?.IndexOf("Permission", System.StringComparison.Ordinal) >= 0
    || reason?.IndexOf("kIOReturnNotPrivileged", System.StringComparison.Ordinal) >= 0
    || reason?.IndexOf("kIOReturnNotPermitted", System.StringComparison.Ordinal) >= 0;

  private static bool IsResourceBusy(string reason) =>
    reason?.IndexOf("kIOReturnExclusiveAccess", System.StringComparison.Ordinal) >= 0
    || reason?.IndexOf("kIOReturnBusy", System.StringComparison.Ordinal) >= 0;

  private static void OpenSystemSettings()
  {
    Application.OpenURL(SettingsUrl);
  }
}
