using System.Collections.Generic;
using System.Text.RegularExpressions;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

public static class FfmpegCameraDevices
{
  public static List<WebcamDevice> Parse(IEnumerable<string> output)
  {
    var devices = new List<WebcamDevice>();
    bool awaitingAlternative = false;
    foreach (string line in output)
    {
      Match named = Regex.Match(line, "\"(.*)\" \\(video\\)");
      if (named.Success)
      {
        devices.Add(new WebcamDevice { Id = named.Groups[1].Value, Name = named.Groups[1].Value });
        awaitingAlternative = true;
      }
      else if (line.Contains("(audio)"))
        awaitingAlternative = false;
      else if (awaitingAlternative)
      {
        Match alternative = Regex.Match(line, "Alternative name \"(.*)\"");
        if (alternative.Success)
        {
          devices[devices.Count - 1].Id = alternative.Groups[1].Value;
          awaitingAlternative = false;
        }
      }
    }
    return devices;
  }
}
