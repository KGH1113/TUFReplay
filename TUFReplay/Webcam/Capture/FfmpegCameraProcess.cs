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
  // Camera frames can be skipped with their timing intact. Let foreground
  // game/input work win CPU contention instead of causing gameplay stutters.
  public static void PreferGameplay(Process process) => SetPriority(process, ProcessPriorityClass.BelowNormal);

  // An encoder opening its session ahead of a run also yields to the camera
  // capture, which a hardware encoder's multi-second startup can otherwise delay.
  public static void PreferCapture(Process process) => SetPriority(process, ProcessPriorityClass.Idle);

  private static void SetPriority(Process process, ProcessPriorityClass priority)
  {
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return;
    try
    {
      process.PriorityClass = priority;
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
