using System;
using System.Diagnostics;
using System.Linq;
using TUFReplay.Shared.Media;

namespace TUFReplay;

public static class FfmpegApi
{
  /// <summary>Reports a verified shared installation from another mod. Safe from any thread.</summary>
  public static void NotifyInstallationCompleted(string platform, string executablePath) =>
    FfmpegInstallCoordinator.NotifySharedInstallationCompleted(platform, executablePath);

  internal static void NotifyOtherMod(string platform, string executablePath)
  {
    try
    {
      AppDomain
        .CurrentDomain.GetAssemblies()
        .FirstOrDefault(assembly => assembly.GetName().Name == "OrbitRender")
        ?.GetType("OrbitRender.FfmpegApi")
        ?.GetMethod(nameof(NotifyInstallationCompleted))
        ?.Invoke(null, new object[] { platform, executablePath });
    }
    catch (Exception error)
    {
      Trace.TraceWarning("[FFmpeg] OrbitRender completion notification failed: " + error);
    }
  }
}
