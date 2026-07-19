using System;
using System.IO;

namespace TUFReplay.Infrastructure.Unity;

public static class ReplayMicrophonePlaybackFiles
{
  private static string DirectoryPath => Path.Combine(Main.Instance.Path, "Data", "MicrophonePlayback");

  public static void Initialize()
  {
    Directory.CreateDirectory(DirectoryPath);
    foreach (string path in Directory.GetFiles(DirectoryPath))
      Delete(path);
  }

  public static string ForOperation(string operationId)
  {
    return Path.Combine(DirectoryPath, operationId + ".wav");
  }

  public static void Delete(string path)
  {
    try
    {
      if (!string.IsNullOrEmpty(path) && File.Exists(path))
        File.Delete(path);
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Replay/Microphone] Temp file cleanup failed. error=" + exception.Message);
    }
  }
}
