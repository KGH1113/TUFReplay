using System;
using System.Runtime.InteropServices;
using TUFReplay.Shared.Unity;

namespace TUFReplay.Recording.Input;

internal static class MacOsInputMonitoringAccess
{
  private static bool _initialized;
  private static string _failureReason;

  public static string FailureReason => _failureReason;

  public static void InitializeAtGameStart()
  {
    if (_initialized || !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return;
    _initialized = true;
    try
    {
      if (!MacOsIoHidNativeLibrary.TryLoad(out MacOsIoHidNativeLibrary library, out string loadFailure))
      {
        NotifyUnavailable(loadFailure);
        return;
      }

      MacOsInputAccess access = library.CheckAccess();
      if (access == MacOsInputAccess.Unknown)
        access = library.RequestAccess();
      if (access != MacOsInputAccess.Granted)
        NotifyUnavailable("input_monitoring_" + access.ToString().ToLowerInvariant());
    }
    catch (Exception exception)
    {
      NotifyUnavailable("input_monitoring_check_failed: " + exception.Message);
    }
  }

  public static void NotifyUnavailable(string reason)
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return;
    _failureReason = string.IsNullOrWhiteSpace(reason) ? "input_monitoring_unavailable" : reason;
    Main.Instance?.Log("[Recording/Input] macOS IOHID unavailable. reason=" + _failureReason);
    UnityMainThread.Post(() => MacOsInputPermissionNotice.Show(_failureReason));
  }
}
