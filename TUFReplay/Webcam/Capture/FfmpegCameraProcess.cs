using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TUFReplay.Shared.Capture;

namespace TUFReplay.Webcam.Capture;

internal static class FfmpegCameraProcess
{
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
