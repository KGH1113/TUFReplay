using System;
using System.IO;
using System.Runtime.InteropServices;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Shared.Media;
using UnityModManagerNet;

namespace TUFReplay.Shared.Downloads;

internal static class DownloadCenterCoordinator
{
  public static event Action Changed;

  private static void NotifyChanged() => Changed?.Invoke();

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
      _renderer.Changed += NotifyChanged;
    }
    catch (Exception error)
    {
      _error = error.Message;
    }
  }

  // Called on the game thread only for the inexpensive UMM state lookup.
  public static DownloadsState Status() =>
    new DownloadsState
    {
      Renderer = IpcDtoMapper.Map(
        _renderer?.Snapshot(UnityModManager.FindMod(ManagedRendererInstaller.ModId)?.Active == true)
          ?? new RendererInstallState
          {
            Status = "failed",
            Error = _error ?? "Enable TUFReplay and restart the game.",
            ErrorCode = "downloads_unavailable",
          }
      ),
      Ffmpeg = IpcDtoMapper.Map(FfmpegInstallCoordinator.Status()),
      CameraNeedsFfmpeg = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
    };

  public static DownloadsState RequestRenderer()
  {
    _renderer?.Request();
    return Status();
  }

  public static DownloadsState ConfirmRenderer()
  {
    _renderer?.Confirm();
    return Status();
  }

  public static DownloadsState CancelRenderer()
  {
    _renderer?.Cancel();
    return Status();
  }

  public static void Shutdown()
  {
    if (_renderer != null)
      _renderer.Changed -= NotifyChanged;
    _renderer?.Dispose();
    _renderer = null;
    _error = null;
  }
}
