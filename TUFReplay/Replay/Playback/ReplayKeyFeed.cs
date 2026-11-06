using System.Collections.Generic;
using System.Runtime.InteropServices;
using TUFReplay.ModApi.Playback;
using TUFReplay.Replay.Models;
using TUFReplay.Shared.NativeInput;
using UnityEngine;

namespace TUFReplay.Replay.Playback;

/// <summary>
/// Hands a replay's recorded keys to mods through <see cref="ReplayApi"/>, at their recorded
/// times on the session clock. It runs beside the OS input the replay still sends, and does not
/// wait for the game window's focus: a key viewer shows every recorded key.
/// </summary>
internal sealed class ReplayKeyFeed
{
  // On Windows the recorded virtual-key codes are the codes SkyHook reports, so mods that bind
  // native codes can match them. macOS records virtual key codes, but SkyHook reports USB HID
  // usages on newer game versions, so a native code would name the wrong key there.
  private static readonly bool SendsNativeKeys = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

  private readonly long[] _timesUs;
  private readonly KeyCode[] _keys;
  private readonly int[] _nativeKeys;
  private readonly bool[] _downs;
  private readonly Dictionary<int, KeyCode> _held = new Dictionary<int, KeyCode>();
  private readonly List<int> _release = new List<int>();
  private int _next;

  /// <param name="inputs">The run's inputs, normalized for playback on this machine.</param>
  public ReplayKeyFeed(IReadOnlyList<RecordedInput> inputs)
  {
    var times = new List<long>(inputs.Count);
    var keys = new List<KeyCode>(inputs.Count);
    var natives = new List<int>(inputs.Count);
    var downs = new List<bool>(inputs.Count);
    foreach (RecordedInput input in inputs)
    {
      NativeInputKeyCodeMapper.TryGetPlaybackKeyCode(input, out KeyCode key);
      // A key only a native code names still reaches mods that bind native codes.
      if (key == KeyCode.None && (!SendsNativeKeys || input.Key < 0))
        continue;
      times.Add(input.TimeUs);
      keys.Add(key);
      natives.Add(input.Key);
      downs.Add(input.Down);
    }
    _timesUs = times.ToArray();
    _keys = keys.ToArray();
    _nativeKeys = natives.ToArray();
    _downs = downs.ToArray();
  }

  public int Count => _timesUs.Length;

  /// <summary>
  /// Sends every key recorded up to <paramref name="replayTimeUs"/>. A key that is late arrives
  /// with the session time it should have had, measured back from <paramref name="nowTicks"/>.
  /// </summary>
  public void AdvanceTo(long replayTimeUs, long nowTicks)
  {
    while (_next < _timesUs.Length && _timesUs[_next] <= replayTimeUs)
    {
      long lateTicks = (replayTimeUs - _timesUs[_next]) * 10;
      long ticks = nowTicks - lateTicks;
      Send(_next, ticks < 0 ? 0 : ticks);
      _next++;
    }
  }

  /// <summary>Moves to <paramref name="replayTimeUs"/> without sending the keys in between; held keys are released.</summary>
  public void SkipTo(long replayTimeUs, long nowTicks)
  {
    ReleaseAll(nowTicks);
    _next = 0;
    while (_next < _timesUs.Length && _timesUs[_next] <= replayTimeUs)
      _next++;
  }

  /// <summary>Releases every key the replay still holds.</summary>
  public void ReleaseAll(long nowTicks)
  {
    if (_held.Count == 0)
      return;
    _release.Clear();
    _release.AddRange(_held.Keys);
    foreach (int native in _release)
      ReplaySessions.Key(_held[native], SendsNativeKeys ? native : null, false, nowTicks);
    _held.Clear();
  }

  private void Send(int index, long ticks)
  {
    int native = _nativeKeys[index];
    bool down = _downs[index];
    if (down)
      _held[native] = _keys[index];
    else
      _held.Remove(native);
    ReplaySessions.Key(_keys[index], SendsNativeKeys ? native : null, down, ticks);
  }
}
