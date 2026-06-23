using System.Collections.Generic;
using SkyHook;
using TUFReplay.Replay;
using UnityEngine;

namespace TUFReplay.Recording;

public static class RDInputRecorder
{
  private static readonly KeyCode[] KeyCodes = CreateKeyCodes();
  private static readonly HashSet<KeyCode> HeldKeys = new HashSet<KeyCode>();
  private static readonly HashSet<KeyCode> AsyncHeldKeys = new HashSet<KeyCode>();
  private static readonly HashSet<KeyCode> UnityHeldKeys = new HashSet<KeyCode>();
  private static int _logCount;
  private static int _lastScanFrame = -1;

  public static void Reset()
  {
    HeldKeys.Clear();
    AsyncHeldKeys.Clear();
    UnityHeldKeys.Clear();
    _logCount = 0;
  }

  public static void CaptureStateKeys(ButtonState state, List<AnyKeyCode> keys)
  {
    RecordingSession session = Recording.Instance?.Session;
    if (!ShouldCapture(session)) return;

    ScanCurrentKeys(session, "UnityInput.Fallback");
    if (keys == null || !IsAsyncKeyboardActive()) return;

    if (state == ButtonState.WentDown)
    {
      CaptureTransitionKeys(session, keys, true, "Async.GetStateKeys.WentDown");
      return;
    }

    if (state == ButtonState.WentUp)
    {
      CaptureTransitionKeys(session, keys, false, "Async.GetStateKeys.WentUp");
    }
  }

  public static void CaptureMainState(ButtonState state, int count)
  {
    RecordingSession session = Recording.Instance?.Session;
    if (!ShouldCapture(session)) return;

    ScanCurrentKeys(session, "UnityInput.Fallback");

    if (IsAsyncKeyboardActive())
    {
      switch (state)
      {
        case ButtonState.WentDown:
          CaptureAsyncKeys(session, AsyncInputManager.keyDownMask, true, "Async.GetMain.WentDown");
          break;
        case ButtonState.WentUp:
          CaptureAsyncKeys(session, AsyncInputManager.keyUpMask, false, "Async.GetMain.WentUp");
          break;
      }

      return;
    }
  }

  public static void FlushHeldReleases(RecordingSession session, string source)
  {
    if (!ShouldCapture(session) || HeldKeys.Count == 0) return;

    double songPosition = IsAsyncKeyboardActive() ? GetAsyncSongPosition() : GetSongPosition();
    List<KeyCode> keys = new List<KeyCode>(HeldKeys);

    foreach (KeyCode keyCode in keys)
    {
      HeldKeys.Remove(keyCode);
      AddInput(session, keyCode, false, songPosition, source);
    }
  }

  private static void CaptureAsyncKeys(
    RecordingSession session,
    IEnumerable<AsyncKeyCode> keys,
    bool down,
    string source
  )
  {
    if (keys == null) return;

    double songPosition = GetAsyncSongPosition();

    foreach (AsyncKeyCode key in keys)
    {
      if (!TryMapKeyCode(key.label, out KeyCode keyCode)) continue;
      SetSourceKeyState(session, AsyncHeldKeys, keyCode, down, songPosition, source);
    }
  }

  private static void CaptureTransitionKeys(RecordingSession session, List<AnyKeyCode> keys, bool down, string source)
  {
    double songPosition = GetAsyncSongPosition();

    foreach (AnyKeyCode key in keys)
    {
      if (!TryGetKeyCode(key, out KeyCode keyCode)) continue;
      SetSourceKeyState(session, AsyncHeldKeys, keyCode, down, songPosition, source);
    }
  }

  private static bool ShouldCapture(RecordingSession session)
  {
    if (session == null || !session.IsRecording) return false;
    if (global::TUFReplay.Replay.Replay.Instance?.Session.IsPlaying == true) return false;
    return true;
  }

  private static void ScanCurrentKeys(RecordingSession session, string source)
  {
    if (_lastScanFrame == Time.frameCount) return;

    _lastScanFrame = Time.frameCount;

    HashSet<KeyCode> currentHeldKeys = new HashSet<KeyCode>();

    foreach (KeyCode keyCode in KeyCodes)
    {
      if (!Input.GetKey(keyCode)) continue;

      currentHeldKeys.Add(keyCode);
      SetSourceKeyState(session, UnityHeldKeys, keyCode, true, GetSongPosition(), source);
    }

    List<KeyCode> releasedKeys = new List<KeyCode>();
    foreach (KeyCode keyCode in UnityHeldKeys)
    {
      if (!currentHeldKeys.Contains(keyCode)) releasedKeys.Add(keyCode);
    }

    foreach (KeyCode keyCode in releasedKeys)
    {
      SetSourceKeyState(session, UnityHeldKeys, keyCode, false, GetSongPosition(), source);
    }
  }

  private static void SetSourceKeyState(
    RecordingSession session,
    HashSet<KeyCode> sourceHeldKeys,
    KeyCode keyCode,
    bool down,
    double songPosition,
    string source
  )
  {
    if (down)
    {
      if (!sourceHeldKeys.Add(keyCode)) return;
      if (HeldKeys.Add(keyCode)) AddInput(session, keyCode, true, songPosition, source);
      return;
    }

    if (!sourceHeldKeys.Remove(keyCode)) return;
    if (AsyncHeldKeys.Contains(keyCode) || UnityHeldKeys.Contains(keyCode)) return;
    if (HeldKeys.Remove(keyCode)) AddInput(session, keyCode, false, songPosition, source);
  }

  private static void AddInput(RecordingSession session, KeyCode keyCode, bool down, string source)
  {
    double songPosition = GetSongPosition();
    AddInput(session, keyCode, down, songPosition, source);
  }

  private static void AddInput(RecordingSession session, KeyCode keyCode, bool down, double songPosition, string source)
  {
    session.AddInput((int)keyCode, down, songPosition);

    if (_logCount < 20 || _logCount % 100 == 0)
    {
      Main.Instance.Log(
        "[Recording] RDInput " +
        source +
        " " +
        (down ? "down " : "up ") +
        keyCode +
        " at " +
        songPosition +
        ", count=" +
        session.InputCount
      );
    }

    _logCount++;
  }

  private static bool IsAsyncKeyboardActive()
  {
    if (!AsyncInputManager.isActive) return false;
    if (RDInput.asyncKeyboard != null && RDInput.asyncKeyboard.isActive) return true;
    if (RDInput.asyncKeyboardLeft != null && RDInput.asyncKeyboardLeft.isActive) return true;
    return RDInput.asyncKeyboardRight != null && RDInput.asyncKeyboardRight.isActive;
  }

  private static double GetAsyncSongPosition()
  {
    scrConductor conductor = ADOBase.conductor;
    if (conductor == null) return 0d;
    if (AsyncInputManager.targetSongTick != 0uL)
    {
      return AsyncInputUtils.GetSongPosition(conductor, AsyncInputManager.targetSongTick);
    }

    return conductor.songposition_minusi;
  }

  private static double GetSongPosition()
  {
    return ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
  }

  private static bool TryGetKeyCode(AnyKeyCode anyKeyCode, out KeyCode keyCode)
  {
    if (anyKeyCode.value is KeyCode unityKeyCode)
    {
      keyCode = unityKeyCode;
      return true;
    }

    if (anyKeyCode.value is AsyncKeyCode asyncKeyCode)
    {
      return TryMapKeyCode(asyncKeyCode.label, out keyCode);
    }

    keyCode = KeyCode.None;
    return false;
  }

  private static bool TryMapKeyCode(KeyLabel label, out KeyCode keyCode)
  {
    switch (label)
    {
      case KeyLabel.Grave:
        keyCode = KeyCode.BackQuote;
        return true;
      case KeyLabel.Equal:
        keyCode = KeyCode.Equals;
        return true;
      case KeyLabel.LeftBrace:
        keyCode = KeyCode.LeftBracket;
        return true;
      case KeyLabel.RightBrace:
        keyCode = KeyCode.RightBracket;
        return true;
      case KeyLabel.BackSlash:
        keyCode = KeyCode.Backslash;
        return true;
      case KeyLabel.Apostrophe:
        keyCode = KeyCode.Quote;
        return true;
      case KeyLabel.Enter:
        keyCode = KeyCode.Return;
        return true;
      case KeyLabel.LShift:
        keyCode = KeyCode.LeftShift;
        return true;
      case KeyLabel.RShift:
        keyCode = KeyCode.RightShift;
        return true;
      case KeyLabel.LControl:
        keyCode = KeyCode.LeftControl;
        return true;
      case KeyLabel.RControl:
        keyCode = KeyCode.RightControl;
        return true;
      case KeyLabel.LAlt:
        keyCode = KeyCode.LeftAlt;
        return true;
      case KeyLabel.RAlt:
        keyCode = KeyCode.RightAlt;
        return true;
      case KeyLabel.ArrowLeft:
        keyCode = KeyCode.LeftArrow;
        return true;
      case KeyLabel.ArrowRight:
        keyCode = KeyCode.RightArrow;
        return true;
      case KeyLabel.ArrowUp:
        keyCode = KeyCode.UpArrow;
        return true;
      case KeyLabel.ArrowDown:
        keyCode = KeyCode.DownArrow;
        return true;
      case KeyLabel.KeypadSlash:
        keyCode = KeyCode.KeypadDivide;
        return true;
      case KeyLabel.KeypadAsterisk:
        keyCode = KeyCode.KeypadMultiply;
        return true;
      case KeyLabel.KeypadDot:
        keyCode = KeyCode.KeypadPeriod;
        return true;
      case KeyLabel.KeypadEnter:
        keyCode = KeyCode.KeypadEnter;
        return true;
    }

    if (System.Enum.TryParse(label.ToString(), out keyCode)) return true;

    keyCode = KeyCode.None;
    return false;
  }

  private static KeyCode[] CreateKeyCodes()
  {
    List<KeyCode> keyCodes = new List<KeyCode>();

    foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
    {
      if (keyCode > KeyCode.Mouse6) break;
      if (keyCode == KeyCode.None || keyCode == KeyCode.WheelUp || keyCode == KeyCode.WheelDown) continue;

      keyCodes.Add(keyCode);
    }

    return keyCodes.ToArray();
  }
}
