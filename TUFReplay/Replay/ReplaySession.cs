using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using SkyHook;
using TUFReplay.Shared;
using UnityEngine;

namespace TUFReplay.Replay;

class ReplaySession
{
  private readonly List<ReplayInputEvent> _events = new List<ReplayInputEvent>();
  private readonly HashSet<KeyCode> _heldKeys = new HashSet<KeyCode>();
  private readonly HashSet<AsyncKeyCode> _heldAsyncKeys = new HashSet<AsyncKeyCode>();
  private readonly List<KeyCode> _wentDownKeys = new List<KeyCode>();
  private readonly List<KeyCode> _wentUpKeys = new List<KeyCode>();
  private int _eventIndex;
  private int _lastSyncedFrame = -1;
  private int _startedFrame;
  private int _lastIdleLogFrame = -1;
  private int _lastCountLogFrame = -1;
  private int _lastQueueLogFrame = -1;
  private bool _pendingPlaybackPositionSync;

  private static readonly FieldInfo SkyHookEventTimeSecField = typeof(SkyHookEvent).GetField(nameof(SkyHookEvent.TimeSec));
  private static readonly FieldInfo SkyHookEventTimeSubsecNanoField = typeof(SkyHookEvent).GetField(nameof(SkyHookEvent.TimeSubsecNano));
  private static readonly FieldInfo SkyHookEventTypeField = typeof(SkyHookEvent).GetField(nameof(SkyHookEvent.Type));
  private static readonly FieldInfo SkyHookEventLabelField = typeof(SkyHookEvent).GetField(nameof(SkyHookEvent.Label));
  private static readonly FieldInfo SkyHookEventKeyField = typeof(SkyHookEvent).GetField(nameof(SkyHookEvent.Key));

  public bool IsPlaying { get; private set; }
  public string RecordId { get; private set; }
  public bool UsesAsyncInputPlayback
  {
    get
    {
      if (!IsPlaying || !AsyncInputManager.isActive) return false;
      if (RDInput.asyncKeyboard != null && RDInput.asyncKeyboard.isActive) return true;
      if (RDInput.asyncKeyboardLeft != null && RDInput.asyncKeyboardLeft.isActive) return true;
      return RDInput.asyncKeyboardRight != null && RDInput.asyncKeyboardRight.isActive;
    }
  }

  public bool CanInjectInput()
  {
    if (!IsPlaying) return false;
    if (ReplayService.CanInjectInput(out string reason)) return true;

    Stop("input injection disabled: " + reason);
    ReplayService.ClearActiveContext();
    return false;
  }

  public void Start(PlayRecord record)
  {
    Stop("starting another replay");

    if (record == null || record.InputCsv == null)
    {
      Replay.LogDebug("Start failed: record input is empty.");
      return;
    }

    double? gameplayStartSongPosition = GetGameplayStartSongPosition(record.MetaJson);

    _events.AddRange(ParseInputCsv(record.InputCsv));
    _events.Sort((a, b) => a.SongPosition.CompareTo(b.SongPosition));
    _eventIndex = GetStartEventIndex(_events, gameplayStartSongPosition);
    _lastSyncedFrame = -1;
    _lastIdleLogFrame = -1;
    _lastCountLogFrame = -1;
    _lastQueueLogFrame = -1;
    _pendingPlaybackPositionSync = true;
    _startedFrame = Time.frameCount;
    RecordId = record.Id;
    IsPlaying = _events.Count > 0;

    if (!IsPlaying)
    {
      Replay.LogDebug("Start failed: parsed 0 input events. recordId=" + record.Id + ", inputBytes=" + record.InputCsv.Length);
      return;
    }

    Replay.LogDebug(
      "Started. recordId=" + record.Id +
      ", tufLevelId=" + record.TufLevelId +
      ", inputBytes=" + record.InputCsv.Length +
      ", events=" + _events.Count +
      ", startEventIndex=" + _eventIndex +
      ", gameplayStartSongPosition=" + FormatNullableDouble(gameplayStartSongPosition) +
      ", first=" + FormatEvent(_events[0]) +
      ", firstReplay=" + (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none") +
      ", last=" + FormatEvent(_events[_events.Count - 1])
    );
  }

  public void Stop()
  {
    Stop("manual stop");
  }

  public void Stop(string reason)
  {
    if (IsPlaying || RecordId != null)
    {
      Replay.LogDebug(
        "Stopped. reason=" + reason +
        ", recordId=" + (RecordId ?? "none") +
        ", eventIndex=" + _eventIndex +
        "/" + _events.Count +
        ", held=" + FormatKeys(_heldKeys)
      );
    }

    IsPlaying = false;
    RecordId = null;
    _events.Clear();
    _heldKeys.Clear();
    ClearReplayAsyncKeys();
    NativeKeySender.ReleaseAll();
    _wentDownKeys.Clear();
    _wentUpKeys.Clear();
    _eventIndex = 0;
    _lastSyncedFrame = -1;
    _pendingPlaybackPositionSync = false;
  }

  public int GetMainCount(ButtonState state)
  {
    EnsureInputStateSynced();

    int result = state switch
    {
      ButtonState.WentDown => _wentDownKeys.Count,
      ButtonState.WentUp => _wentUpKeys.Count,
      ButtonState.IsDown => _heldKeys.Count,
      _ => 0
    };

    LogCountQuery(state, result);
    return result;
  }

  public void AddStateKeys(ButtonState state, List<AnyKeyCode> keys)
  {
    EnsureInputStateSynced();

    switch (state)
    {
      case ButtonState.WentDown:
        AddKeys(keys, _wentDownKeys);
        break;
      case ButtonState.WentUp:
        AddKeys(keys, _wentUpKeys);
        break;
      case ButtonState.IsDown:
        foreach (KeyCode key in _heldKeys) keys.Add(new AnyKeyCode(key));
        break;
    }
  }

  public bool IsHeld(KeyCode keyCode)
  {
    EnsureInputStateSynced();
    return _heldKeys.Contains(keyCode);
  }

  public void ProcessAsyncInputFrame(scrController controller)
  {
    if (!IsPlaying || controller == null) return;

    ClearTransientAsyncMasks();

    ulong frameTick = AsyncInputManager.currFrameTick;
    bool processedEvent = false;

    while (_eventIndex < _events.Count)
    {
      ulong eventTick = GetEventTick(_events[_eventIndex].SongPosition);
      if (eventTick > frameTick) break;

      ClearTransientAsyncMasks();
      _wentDownKeys.Clear();
      _wentUpKeys.Clear();

      while (_eventIndex < _events.Count && GetEventTick(_events[_eventIndex].SongPosition) == eventTick)
      {
        ApplyAsyncEvent(_events[_eventIndex++]);
      }

      ProcessPlayersAtTick(eventTick);
      processedEvent = true;

      if (_wentDownKeys.Count + _wentUpKeys.Count > 0)
      {
        Replay.LogDebug(
          "Async event frame=" + Time.frameCount +
          ", eventTick=" + eventTick +
          ", songPosition=" + GetSongPositionFromTick(eventTick).ToString("R", CultureInfo.InvariantCulture) +
          ", eventIndex=" + _eventIndex + "/" + _events.Count +
          ", down=" + FormatKeys(_wentDownKeys) +
          ", up=" + FormatKeys(_wentUpKeys) +
          ", held=" + FormatKeys(_heldKeys)
        );
      }
    }

    if (!processedEvent)
    {
      ProcessPlayersAtTick(frameTick);

      if (Time.frameCount - _startedFrame < 10 || Time.frameCount - _lastIdleLogFrame >= 120)
      {
        _lastIdleLogFrame = Time.frameCount;
        Replay.LogDebug(
          "Async waiting frame=" + Time.frameCount +
          ", frameTick=" + frameTick +
          ", songPosition=" + GetSongPositionFromTick(frameTick).ToString("R", CultureInfo.InvariantCulture) +
          ", next=" + (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none") +
          ", eventIndex=" + _eventIndex + "/" + _events.Count +
          ", held=" + FormatKeys(_heldKeys)
        );
      }
    }
  }

  public void EnqueueDueAsyncInputEvents()
  {
    if (!IsPlaying) return;

    scrConductor conductor = ADOBase.conductor;
    if (conductor == null || conductor.song == null)
    {
      if (Time.frameCount - _lastQueueLogFrame >= 120)
      {
        _lastQueueLogFrame = Time.frameCount;
        Replay.LogDebug("Queue waiting: conductor is not ready. eventIndex=" + _eventIndex + "/" + _events.Count);
      }
      return;
    }

    ulong frameTick = AsyncInputManager.currFrameTick;
    double songPosition = conductor.songposition_minusi;
    if (_pendingPlaybackPositionSync && !SynchronizePlaybackPosition(songPosition)) return;

    BeginAsyncStateFrame();
    int enqueued = 0;

    while (_eventIndex < _events.Count)
    {
      ReplayInputEvent input = _events[_eventIndex];
      if (input.SongPosition > songPosition) break;

      ulong eventTick = GetEventWallTick(input.SongPosition);
      if (TryCreateSkyHookEvent(input, eventTick, out SkyHookEvent skyHookEvent))
      {
        AsyncInputManager.keyQueue.Enqueue(skyHookEvent);
        TrackQueuedInput(input);
        MirrorInputToOS(input);
        enqueued++;
      }

      _eventIndex++;
    }

    if (enqueued > 0)
    {
      Replay.LogDebug(
        "Queued async input frame=" + Time.frameCount +
        ", frameTick=" + frameTick +
        ", songPosition=" + songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", enqueued=" + enqueued +
        ", eventIndex=" + _eventIndex + "/" + _events.Count +
        ", next=" + (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none")
      );
      return;
    }

    if (Time.frameCount - _startedFrame < 10 || Time.frameCount - _lastQueueLogFrame >= 120)
    {
      _lastQueueLogFrame = Time.frameCount;
      Replay.LogDebug(
        "Queue waiting frame=" + Time.frameCount +
        ", frameTick=" + frameTick +
        ", songPosition=" + songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", next=" + (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none") +
        ", eventIndex=" + _eventIndex + "/" + _events.Count
      );
    }
  }

  private void EnsureSynced()
  {
    if (!IsPlaying) return;
    if (_lastSyncedFrame == Time.frameCount) return;

    _lastSyncedFrame = Time.frameCount;
    _wentDownKeys.Clear();
    _wentUpKeys.Clear();

    double songPosition = ADOBase.conductor != null ? ADOBase.conductor.songposition_minusi : 0d;
    double leadSeconds = Replay.Settings?.InputLeadSeconds ?? 0d;
    double replaySongPosition = songPosition + leadSeconds;

    while (_eventIndex < _events.Count && _events[_eventIndex].SongPosition <= replaySongPosition)
    {
      ReplayInputEvent input = _events[_eventIndex++];
      KeyCode keyCode = (KeyCode)input.KeyCode;

      if (input.Down)
      {
        if (_heldKeys.Add(keyCode)) _wentDownKeys.Add(keyCode);
      }
      else
      {
        if (_heldKeys.Remove(keyCode)) _wentUpKeys.Add(keyCode);
      }
    }

    int consumed = _wentDownKeys.Count + _wentUpKeys.Count;
    if (consumed > 0)
    {
      Replay.LogDebug(
        "Synced frame=" + Time.frameCount +
        ", songPosition=" + songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", replaySongPosition=" + replaySongPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", lead=" + leadSeconds.ToString("R", CultureInfo.InvariantCulture) +
        ", consumed=" + consumed +
        ", eventIndex=" + _eventIndex + "/" + _events.Count +
        ", down=" + FormatKeys(_wentDownKeys) +
        ", up=" + FormatKeys(_wentUpKeys) +
        ", held=" + FormatKeys(_heldKeys)
      );
      return;
    }

    if (Time.frameCount - _startedFrame < 10 || Time.frameCount - _lastIdleLogFrame >= 120)
    {
      _lastIdleLogFrame = Time.frameCount;
      Replay.LogDebug(
        "Waiting frame=" + Time.frameCount +
        ", songPosition=" + songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", replaySongPosition=" + replaySongPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", lead=" + leadSeconds.ToString("R", CultureInfo.InvariantCulture) +
        ", next=" + (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none") +
        ", eventIndex=" + _eventIndex + "/" + _events.Count +
        ", held=" + FormatKeys(_heldKeys)
      );
    }
  }

  private void EnsureInputStateSynced()
  {
    if (UsesAsyncInputPlayback)
    {
      BeginAsyncStateFrame();
      return;
    }

    EnsureSynced();
  }

  private void BeginAsyncStateFrame()
  {
    if (_lastSyncedFrame == Time.frameCount) return;

    _lastSyncedFrame = Time.frameCount;
    _wentDownKeys.Clear();
    _wentUpKeys.Clear();
  }

  private void LogCountQuery(ButtonState state, int result)
  {
    if (Time.frameCount - _startedFrame >= 10 && Time.frameCount - _lastCountLogFrame < 120) return;
    if (state != ButtonState.WentDown) return;

    _lastCountLogFrame = Time.frameCount;
    Replay.LogDebug(
      "RDInput override active. frame=" + Time.frameCount +
      ", wentDown=" + result +
      ", held=" + _heldKeys.Count +
      ", wentUp=" + _wentUpKeys.Count +
      ", eventIndex=" + _eventIndex + "/" + _events.Count
    );
  }

  private static void AddKeys(List<AnyKeyCode> target, List<KeyCode> source)
  {
    foreach (KeyCode key in source) target.Add(new AnyKeyCode(key));
  }

  private void ApplyAsyncEvent(ReplayInputEvent input)
  {
    KeyCode keyCode = (KeyCode)input.KeyCode;
    if (!TryCreateAsyncKeyCode(keyCode, out AsyncKeyCode asyncKeyCode)) return;

    if (input.Down)
    {
      if (!_heldAsyncKeys.Add(asyncKeyCode)) return;

      _heldKeys.Add(keyCode);
      _wentDownKeys.Add(keyCode);
      AsyncInputManager.keyMask.Add(asyncKeyCode);
      AsyncInputManager.keyDownMask.Add(asyncKeyCode);
      AsyncInputManager.frameDependentKeyMask.Add(asyncKeyCode);
      AsyncInputManager.frameDependentKeyDownMask.Add(asyncKeyCode);
      return;
    }

    _heldAsyncKeys.Remove(asyncKeyCode);
    _heldKeys.Remove(keyCode);
    _wentUpKeys.Add(keyCode);
    AsyncInputManager.keyMask.Remove(asyncKeyCode);
    AsyncInputManager.keyUpMask.Add(asyncKeyCode);
    AsyncInputManager.frameDependentKeyMask.Remove(asyncKeyCode);
    AsyncInputManager.frameDependentKeyUpMask.Add(asyncKeyCode);
  }

  private void TrackQueuedInput(ReplayInputEvent input)
  {
    KeyCode keyCode = (KeyCode)input.KeyCode;
    bool hasAsyncKeyCode = TryCreateAsyncKeyCode(keyCode, out AsyncKeyCode asyncKeyCode);

    if (input.Down)
    {
      if (_heldKeys.Add(keyCode)) _wentDownKeys.Add(keyCode);
      if (hasAsyncKeyCode) _heldAsyncKeys.Add(asyncKeyCode);
      return;
    }

    if (_heldKeys.Remove(keyCode)) _wentUpKeys.Add(keyCode);
    if (hasAsyncKeyCode) _heldAsyncKeys.Remove(asyncKeyCode);
  }

  private static void ProcessPlayersAtTick(ulong tick)
  {
    if (ADOBase.playerManager != null)
    {
      foreach (scrPlayer player in ADOBase.playerManager)
      {
        player.Simulated_PlayerControl_Update(tick);
      }
    }

    AsyncInputManager.lastReportedTargetTick = tick;
  }

  private static void ClearTransientAsyncMasks()
  {
    AsyncInputManager.keyDownMask.Clear();
    AsyncInputManager.keyUpMask.Clear();
    AsyncInputManager.frameDependentKeyDownMask.Clear();
    AsyncInputManager.frameDependentKeyUpMask.Clear();
  }

  private void ClearReplayAsyncKeys()
  {
    foreach (AsyncKeyCode key in _heldAsyncKeys)
    {
      AsyncInputManager.keyMask.Remove(key);
      AsyncInputManager.frameDependentKeyMask.Remove(key);
    }

    _heldAsyncKeys.Clear();
    ClearTransientAsyncMasks();
    ClearPendingAsyncKeyQueue();
  }

  private static void ClearPendingAsyncKeyQueue()
  {
    while (AsyncInputManager.keyQueue.TryDequeue(out _))
    {
    }
  }

  private bool SynchronizePlaybackPosition(double songPosition)
  {
    if (_events.Count == 0)
    {
      _pendingPlaybackPositionSync = false;
      return true;
    }

    double nextSongPosition = _eventIndex < _events.Count
      ? _events[_eventIndex].SongPosition
      : double.PositiveInfinity;

    if (Time.frameCount - _startedFrame <= 2 && songPosition > nextSongPosition + 0.5d)
    {
      Replay.LogDebug(
        "Waiting for song position reset before replay queue. frame=" +
        Time.frameCount +
        ", songPosition=" +
        songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", next=" +
        (_eventIndex < _events.Count ? FormatEvent(_events[_eventIndex]) : "none") +
        ", eventIndex=" +
        _eventIndex +
        "/" +
        _events.Count
      );
      return false;
    }

    if (_eventIndex < _events.Count && songPosition > nextSongPosition + 0.05d)
    {
      int syncedIndex = GetStartEventIndex(_events, songPosition);
      RestoreHeldKeysBeforeIndex(syncedIndex);

      Replay.LogDebug(
        "Synchronized replay start to current song position. frame=" +
        Time.frameCount +
        ", songPosition=" +
        songPosition.ToString("R", CultureInfo.InvariantCulture) +
        ", oldEventIndex=" +
        _eventIndex +
        ", newEventIndex=" +
        syncedIndex +
        "/" +
        _events.Count +
        ", held=" +
        FormatKeys(_heldKeys)
      );

      _eventIndex = syncedIndex;
    }

    _pendingPlaybackPositionSync = false;
    return true;
  }

  private void RestoreHeldKeysBeforeIndex(int eventIndex)
  {
    ClearReplayAsyncKeys();
    _heldKeys.Clear();

    for (int i = 0; i < eventIndex; i++)
    {
      KeyCode keyCode = (KeyCode)_events[i].KeyCode;
      if (!TryCreateAsyncKeyCode(keyCode, out AsyncKeyCode asyncKeyCode)) continue;

      if (_events[i].Down)
      {
        _heldKeys.Add(keyCode);
        _heldAsyncKeys.Add(asyncKeyCode);
      }
      else
      {
        _heldKeys.Remove(keyCode);
        _heldAsyncKeys.Remove(asyncKeyCode);
      }
    }

    foreach (AsyncKeyCode key in _heldAsyncKeys)
    {
      AsyncInputManager.keyMask.Add(key);
      AsyncInputManager.frameDependentKeyMask.Add(key);
    }
  }

  private static ulong GetEventTick(double songPosition)
  {
    scrConductor conductor = ADOBase.conductor;
    if (conductor == null || conductor.song == null) return ulong.MaxValue;

    double pitch = conductor.song.pitch;
    if (Math.Abs(pitch) < 0.000001d) pitch = 1d;

    double tick = ((songPosition + conductor.addoffset) / pitch + conductor.dspTimeSong + scrConductor.calibration_i) * 10000000d;
    return tick <= 0d ? 0uL : (ulong)tick;
  }

  private static ulong GetEventWallTick(double songPosition)
  {
    ulong songTick = GetEventTick(songPosition);
    if (songTick == ulong.MaxValue) return songTick;

    return songTick + AsyncInputManager.offsetTick;
  }

  private static double GetSongPositionFromTick(ulong tick)
  {
    scrConductor conductor = ADOBase.conductor;
    if (conductor == null) return 0d;

    return AsyncInputUtils.GetSongPosition(conductor, tick);
  }

  private static bool TryCreateAsyncKeyCode(KeyCode keyCode, out AsyncKeyCode asyncKeyCode)
  {
    if (TryMapKeyLabel(keyCode, out KeyLabel label))
    {
      asyncKeyCode = new AsyncKeyCode(label);
      return true;
    }

    asyncKeyCode = default;
    return false;
  }

  private static bool TryCreateSkyHookEvent(ReplayInputEvent input, ulong eventTick, out SkyHookEvent skyHookEvent)
  {
    skyHookEvent = default;

    KeyCode keyCode = (KeyCode)input.KeyCode;
    if (!TryMapKeyLabel(keyCode, out KeyLabel label)) return false;

    long unixTicks = (long)eventTick - new DateTime(1970, 1, 1).Ticks;
    if (unixTicks < 0) unixTicks = 0;

    long seconds = unixTicks / 10000000L;
    uint nanos = (uint)((unixTicks % 10000000L) * 100L);

    object boxed = FormatterServices.GetUninitializedObject(typeof(SkyHookEvent));
    SkyHookEventTimeSecField.SetValue(boxed, seconds);
    SkyHookEventTimeSubsecNanoField.SetValue(boxed, nanos);
    SkyHookEventTypeField.SetValue(boxed, input.Down ? SkyHook.EventType.KeyPressed : SkyHook.EventType.KeyReleased);
    SkyHookEventLabelField.SetValue(boxed, label);
    SkyHookEventKeyField.SetValue(boxed, ushort.MaxValue);

    skyHookEvent = (SkyHookEvent)boxed;
    return true;
  }

  private static void MirrorInputToOS(ReplayInputEvent input)
  {
    if (Replay.Settings?.MirrorInputToOS != true) return;

    NativeKeySender.Send((KeyCode)input.KeyCode, input.Down);
  }

  private static bool TryMapKeyLabel(KeyCode keyCode, out KeyLabel label)
  {
    string name = keyCode.ToString();
    switch (keyCode)
    {
      case KeyCode.BackQuote:
        label = KeyLabel.Grave;
        return true;
      case KeyCode.Equals:
        label = KeyLabel.Equal;
        return true;
      case KeyCode.LeftBracket:
        label = KeyLabel.LeftBrace;
        return true;
      case KeyCode.RightBracket:
        label = KeyLabel.RightBrace;
        return true;
      case KeyCode.Backslash:
        label = KeyLabel.BackSlash;
        return true;
      case KeyCode.Quote:
        label = KeyLabel.Apostrophe;
        return true;
      case KeyCode.Return:
        label = KeyLabel.Enter;
        return true;
      case KeyCode.LeftShift:
        label = KeyLabel.LShift;
        return true;
      case KeyCode.RightShift:
        label = KeyLabel.RShift;
        return true;
      case KeyCode.LeftControl:
        label = KeyLabel.LControl;
        return true;
      case KeyCode.RightControl:
        label = KeyLabel.RControl;
        return true;
      case KeyCode.LeftAlt:
        label = KeyLabel.LAlt;
        return true;
      case KeyCode.RightAlt:
        label = KeyLabel.RAlt;
        return true;
      case KeyCode.LeftArrow:
        label = KeyLabel.ArrowLeft;
        return true;
      case KeyCode.RightArrow:
        label = KeyLabel.ArrowRight;
        return true;
      case KeyCode.UpArrow:
        label = KeyLabel.ArrowUp;
        return true;
      case KeyCode.DownArrow:
        label = KeyLabel.ArrowDown;
        return true;
      case KeyCode.KeypadDivide:
        label = KeyLabel.KeypadSlash;
        return true;
      case KeyCode.KeypadMultiply:
        label = KeyLabel.KeypadAsterisk;
        return true;
      case KeyCode.KeypadPeriod:
        label = KeyLabel.KeypadDot;
        return true;
      case KeyCode.KeypadEnter:
        label = KeyLabel.KeypadEnter;
        return true;
    }

    if (Enum.TryParse(name, out label)) return true;

    label = KeyLabel.Unknown;
    return false;
  }

  private static List<ReplayInputEvent> ParseInputCsv(byte[] inputCsv)
  {
    List<ReplayInputEvent> inputs = new List<ReplayInputEvent>();
    string csv = Encoding.UTF8.GetString(inputCsv);
    string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

    foreach (string line in lines)
    {
      string[] columns = line.Split(',');
      if (columns.Length < 3) continue;
      if (!TryParseDouble(columns[0], out double songPosition)) continue;
      if (!int.TryParse(columns[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int keyCode)) continue;

      inputs.Add(new ReplayInputEvent
      {
        SongPosition = songPosition,
        KeyCode = keyCode,
        Down = columns[2] == "1" || columns[2].Equals("true", StringComparison.OrdinalIgnoreCase)
      });
    }

    return inputs;
  }

  private static bool TryParseDouble(string value, out double result)
  {
    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result)) return true;
    return double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
  }

  private static double? GetGameplayStartSongPosition(string metaJson)
  {
    try
    {
      ReplayRecordMeta meta = JsonConvert.DeserializeObject<ReplayRecordMeta>(metaJson);
      return meta?.gameplayStartSongPosition;
    }
    catch (Exception e)
    {
      Replay.LogDebug("Failed to parse replay meta: " + e.Message);
      return null;
    }
  }

  private static int GetStartEventIndex(List<ReplayInputEvent> events, double? gameplayStartSongPosition)
  {
    if (!gameplayStartSongPosition.HasValue) return 0;

    for (int i = 0; i < events.Count; i++)
    {
      if (events[i].SongPosition >= gameplayStartSongPosition.Value) return i;
    }

    return events.Count;
  }

  private static string FormatEvent(ReplayInputEvent input)
  {
    if (input == null) return "null";

    return input.SongPosition.ToString("R", CultureInfo.InvariantCulture) +
           "/" +
           ((KeyCode)input.KeyCode) +
           "/" +
           (input.Down ? "down" : "up");
  }

  private static string FormatNullableDouble(double? value)
  {
    return value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "none";
  }

  private static string FormatKeys(IEnumerable<KeyCode> keys)
  {
    List<string> names = new List<string>();
    int count = 0;

    foreach (KeyCode key in keys)
    {
      count++;
      if (names.Count < 8) names.Add(key.ToString());
    }

    if (count > names.Count) names.Add("+" + (count - names.Count));
    return names.Count == 0 ? "[]" : "[" + string.Join(",", names.ToArray()) + "]";
  }
}
