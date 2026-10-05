using System;
using System.IO;
using System.Runtime.InteropServices;
using TUFReplay.Shared.Media;
using UnityModManagerNet;

namespace TUFReplay.Shared.Downloads;

internal static class DownloadCenterCoordinator
{
  private static ManagedRendererInstaller _renderer;
  private static string _error;

  public static void Initialize()
  {
    try
    {
      _renderer = new ManagedRendererInstaller(
        Directory
          .GetParent(Main.Instance.InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
          .FullName
      );
    }
    catch (Exception error)
    {
      _error = error.Message;
    }
  }

  // Called on the game thread only for the inexpensive UMM state lookup.
  public static object Status() =>
    new
    {
      Renderer = _renderer?.Snapshot(UnityModManager.FindMod(ManagedRendererInstaller.ModId)?.Active == true)
        ?? new RendererInstallState
        {
          Status = "failed",
          Error = _error ?? "Enable TUFReplay and restart the game.",
          ErrorCode = "downloads_unavailable",
        },
      Ffmpeg = FfmpegInstallCoordinator.Status(),
      CameraNeedsFfmpeg = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
    };

  public static object RequestRenderer()
  {
    _renderer?.Request();
    return Status();
  }

  public static object ConfirmRenderer()
  {
    _renderer?.Confirm();
    return Status();
  }

  public static object CancelRenderer()
  {
    _renderer?.Cancel();
    return Status();
  }

  public static void Shutdown()
  {
    _renderer?.Dispose();
    _renderer = null;
    _error = null;
  }
}
