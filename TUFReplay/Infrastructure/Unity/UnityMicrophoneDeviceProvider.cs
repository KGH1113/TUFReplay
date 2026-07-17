using System;
using System.Collections.Generic;
using TUFReplay.Domain.Microphone;
using UnityEngine;

namespace TUFReplay.Infrastructure.Unity;

public static class UnityMicrophoneDeviceProvider
{
  public static List<MicrophoneDeviceInfo> ListDevices()
  {
    var devices = new List<MicrophoneDeviceInfo>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    string[] names = UnityEngine.Microphone.devices ?? new string[0];
    foreach (string rawName in names)
    {
      string name = rawName?.Trim();
      if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
        continue;

      UnityEngine.Microphone.GetDeviceCaps(name, out int minFrequency, out int maxFrequency);
      devices.Add(
        new MicrophoneDeviceInfo
        {
          Id = name,
          Name = name,
          MinFrequency = minFrequency,
          MaxFrequency = maxFrequency,
        }
      );
    }
    return devices;
  }
}
