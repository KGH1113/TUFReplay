using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TUFReplay.Microphone.Models;
using TUFReplay.Shared.Native;

namespace TUFReplay.Microphone.Devices;

// Finds Bluetooth capture devices so the player can be warned that opening one
// switches the headset to its hands-free profile and lowers game audio quality.
// macOS reports the transport from the helper. On Windows the native library walks
// the endpoint topology through COM, so per-name results are cached.
internal static class MicrophoneBluetoothDetector
{
  private static readonly object Gate = new object();
  private static readonly Dictionary<string, bool> EndpointCache = new Dictionary<string, bool>(StringComparer.Ordinal);

  public static bool IsBluetoothEndpoint(string endpointName)
  {
    if (string.IsNullOrEmpty(endpointName) || !TryGetWindowsLibrary(out WindowsNativeLibrary library))
      return false;
    lock (Gate)
    {
      if (EndpointCache.TryGetValue(endpointName, out bool cached))
        return cached;
    }
    bool bluetooth = Query(library, endpointName);
    lock (Gate)
      EndpointCache[endpointName] = bluetooth;
    return bluetooth;
  }

  public static bool IsDefaultBluetooth(IReadOnlyList<MicrophoneDeviceInfo> devices)
  {
    if (devices != null)
    {
      foreach (MicrophoneDeviceInfo device in devices)
      {
        if (device.IsDefault)
          return device.Bluetooth;
      }
    }
    // The default recording endpoint can change at any time; it is not cached.
    return TryGetWindowsLibrary(out WindowsNativeLibrary library) && Query(library, null);
  }

  // The device a run records from: the selected one, or the system default.
  public static bool IsInUseBluetooth(string selectedDeviceId, IReadOnlyList<MicrophoneDeviceInfo> devices)
  {
    if (string.IsNullOrEmpty(selectedDeviceId))
      return IsDefaultBluetooth(devices);
    if (devices != null)
    {
      foreach (MicrophoneDeviceInfo device in devices)
      {
        if (string.Equals(device.Id, selectedDeviceId, StringComparison.Ordinal))
          return device.Bluetooth;
      }
    }
    return false;
  }

  private static bool TryGetWindowsLibrary(out WindowsNativeLibrary library)
  {
    library = null;
    return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsNativeLibrary.TryLoad(out library, out _);
  }

  private static bool Query(WindowsNativeLibrary library, string endpointName)
  {
    try
    {
      return library.CaptureTransport(endpointName) == WindowsAudioTransport.Bluetooth;
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] Bluetooth detection failed. error=" + exception.Message);
      return false;
    }
  }
}
