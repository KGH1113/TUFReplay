using System;
using System.Runtime.InteropServices;
using SkyHook;
using TUFReplay.Infrastructure.NativeInput;
using UnityEngine.Events;

namespace TUFReplay.Infrastructure.NativeInput.Capture;

internal sealed class SkyHookNativeInputEventSource : INativeInputEventSource
{
  private readonly UnityAction<SkyHookEvent> _listener;
  private Action<NativeInputTransition> _onTransition;
  private bool _listenerAttached;
  private bool _ownsHook;

  public SkyHookNativeInputEventSource()
  {
    _listener = OnKeyUpdated;
  }

  public string Name => "skyhook-native-events";
  public bool IsRunning => AsyncInputManager.isActive;

  public void Start(Action<NativeInputTransition> onTransition)
  {
    if (onTransition == null)
      throw new ArgumentNullException(nameof(onTransition));
    if (_listenerAttached)
      return;

    _onTransition = onTransition;
    bool wasRunning = IsRunning;
    SkyHookManager.KeyUpdated.AddListener(_listener);
    _listenerAttached = true;

    try
    {
      if (!wasRunning)
      {
        AsyncInputManager.ToggleHook(true);
        _ownsHook = true;
      }
    }
    catch
    {
      SkyHookManager.KeyUpdated.RemoveListener(_listener);
      _listenerAttached = false;
      _onTransition = null;
      _ownsHook = false;
      throw;
    }
  }

  public void Stop()
  {
    if (_listenerAttached)
    {
      SkyHookManager.KeyUpdated.RemoveListener(_listener);
      _listenerAttached = false;
    }

    _onTransition = null;

    if (_ownsHook)
    {
      _ownsHook = false;
      if (AsyncInputManager.isActive && !Persistence.GetChosenAsynchronousInput())
        AsyncInputManager.ToggleHook(false);
    }
  }

  private void OnKeyUpdated(SkyHookEvent inputEvent)
  {
    if (IsMouseButton(inputEvent.Label))
      return;
    if (!TryResolveNativeKey(inputEvent, out int nativeKeyCode, out bool extendedKey))
      return;

    bool down;
    switch (inputEvent.Type)
    {
      case EventType.KeyPressed:
        down = true;
        break;
      case EventType.KeyReleased:
        down = false;
        break;
      default:
        return;
    }

    long timestampNs = inputEvent.TimeSec * 1_000_000_000L + inputEvent.TimeSubsecNano;
    _onTransition?.Invoke(new NativeInputTransition(timestampNs, nativeKeyCode, down, extendedKey));
  }

  private static bool TryResolveNativeKey(SkyHookEvent inputEvent, out int nativeKeyCode, out bool extendedKey)
  {
    nativeKeyCode = 0;
    extendedKey = false;

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      // SkyHook exposes the Windows virtual-key value in Key. Preserve it directly:
      // treating a VK as a USB HID usage turns VK_APPS/HANGUL/HANJA into unrelated keys.
      if (TryResolveWindowsNativeKey(inputEvent.Key, inputEvent.Label, out nativeKeyCode, out extendedKey))
        return true;

      // Retain a label fallback for malformed/older SkyHook events, but never reinterpret
      // a Windows VK as a HID usage.
      if (!NativeInputKeyCodeMapper.TryConvertKeyLabel(inputEvent.Label, out nativeKeyCode))
        return false;
      extendedKey = WindowsNativeInputKey.IsExtended(nativeKeyCode, inputEvent.Label);
      return true;
    }

    return NativeInputKeyCodeMapper.TryConvertKeyLabel(inputEvent.Label, out nativeKeyCode)
      || NativeInputKeyCodeMapper.TryConvertSkyHookHidUsage(inputEvent.Key, out nativeKeyCode);
  }

  internal static bool TryResolveWindowsNativeKey(
    int rawVirtualKey,
    KeyLabel label,
    out int nativeKeyCode,
    out bool extendedKey
  )
  {
    nativeKeyCode = 0;
    extendedKey = false;
    if (rawVirtualKey <= 0 || rawVirtualKey > byte.MaxValue || WindowsNativeInputKey.IsMouseButton(rawVirtualKey))
      return false;

    nativeKeyCode = WindowsNativeInputKey.NormalizeCapturedVirtualKey(rawVirtualKey, label);
    extendedKey = WindowsNativeInputKey.IsExtended(nativeKeyCode, label);
    return true;
  }

  private static bool IsMouseButton(KeyLabel label)
  {
    return label == KeyLabel.MouseLeft
      || label == KeyLabel.MouseMiddle
      || label == KeyLabel.MouseRight
      || label == KeyLabel.MouseX1
      || label == KeyLabel.MouseX2;
  }
}
