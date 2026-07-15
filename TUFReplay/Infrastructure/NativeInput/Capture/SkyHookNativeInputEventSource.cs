using System;
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
    if (
      !NativeInputKeyCodeMapper.TryConvertKeyLabel(inputEvent.Label, out int nativeKeyCode)
      && !NativeInputKeyCodeMapper.TryConvertSkyHookHidUsage(inputEvent.Key, out nativeKeyCode)
    )
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
    _onTransition?.Invoke(new NativeInputTransition(timestampNs, nativeKeyCode, down));
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
