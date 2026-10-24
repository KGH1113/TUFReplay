using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using TUFReplay.Shared.Capture;

namespace TUFReplay.Webcam.Capture;

internal static class FfmpegCameraProcess
{
  public static void PreferGameplay(Process process)
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return;
    try
    {
      // Camera frames can be skipped with their timing intact. Let foreground
      // game/input work win CPU contention instead of causing gameplay stutters.
      process.PriorityClass = ProcessPriorityClass.BelowNormal;
    }
    catch (Win32Exception) { }
    catch (InvalidOperationException) { }
  }

  public static Process Create(string executable, IEnumerable<string> arguments) =>
    new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = executable,
        Arguments = string.Join(" ", arguments.Select(CaptureProcessArguments.Quote)),
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        CreateNoWindow = true,
      },
    };
}
