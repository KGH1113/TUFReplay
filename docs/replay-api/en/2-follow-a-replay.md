# 2. Follow a replay

While a run is replayed, your mod should show the replay instead of the live game: the recorded keys, at the replay's time. A provider gets both through these methods.

```csharp
internal sealed class ReplayFollower
{
    public void OnReplayBegin(bool render) => KeyViewer.Instance.BeginReplay();     // stop live input, copy the counts
    public void OnReplayClock(long ticks) => KeyViewer.Instance.ReplayTicks = ticks;
    public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks) =>
        KeyViewer.Instance.ApplyReplayKey(key, nativeKey, down, ticks);
    public void OnReplayEnd() => KeyViewer.Instance.EndReplay();                  // back to live input
}
```

| Method | When |
| --- | --- |
| `void OnReplayBegin(bool render)` or `void OnReplayBegin()` | Before the level starts. `render` is true when the replay is rendered to video. |
| `void OnReplayClock(long ticks)` | Every frame |
| `void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)` | Each recorded key, at its time |
| `void OnReplayEnd()` | When the replay ends, fails or is replaced |

## During a replay

- Take the replay's keys from `OnReplayKey` and ignore live input. TUFReplay still presses the keys through the OS during in-game replays, for now, so a mod that also listens to live input counts them twice.
- Take time from `OnReplayClock` or `ReplayApi.ReplayTicks`, not from a stopwatch. In a render the game runs slower than the video, so real time is wrong.
- Saving progress, counting attempts or adding to persistent counters during a replay is usually wrong. Check `ReplayApi.IsReplaying`.

```csharp
public static bool IsReplaying { get; }     // between OnReplayBegin and OnReplayEnd
public static bool IsRendering { get; }     // the replay is being rendered to video
public static string ReplayRunId { get; }   // the run being replayed, or null
public static long ReplayTicks { get; }     // the replay clock, 100 ns ticks
```

`IsReplaying` is true from just before `OnReplayBegin` until just before `OnReplayEnd`, so it already reads false inside `OnReplayEnd`. Only one replay or render runs at a time.

## `ticks`

- 100 ns units (`TimeSpan.TicksPerSecond`), from 0 when the replay began. They never go back during a replay.
- In the game they are real time since the replay began. In a render they are the video's time.
- A key's `ticks` is the time it was pressed, which can be slightly before the current clock.

## `nativeKey`

- On Windows, the key's virtual-key code: the same code SkyHook reports there, so a mod that binds native codes matches it.
- Null on macOS, where SkyHook reports USB HID usages on newer game versions while recordings keep macOS key codes, and null in renders for now. Match on `key` then.

## In the game, step by step

- `OnReplayBegin(render: false)` runs before the level starts, before ADOFAI's `scnGame.Play`, so an attempt counter can already tell that this play is a replay.
- `OnReplayClock` runs every frame. `OnReplayKey` delivers each recorded key at its time, whether or not the game window has focus.
- A timeline seek releases held keys and continues from the new time. Keys in between are not sent.
- `OnReplayEnd` runs when the replay ends, fails, or is replaced by another replay. Held keys are released first.

A render makes the same calls at video time. [Step 5](5-render.md) covers what it adds.

## Calls and failures

- Every method runs on Unity's main thread. Per-frame calls allocate nothing on TUFReplay's side.
- An exception from your method is written to the log, at most three times per method for per-frame calls, and the replay or render goes on.

## Mods without ReplayApi

A mod that does not register gets nothing from TUFReplay:
- In renders it sees no replay keys. TUFReplay does not inject keys into SkyHook or Unity's Input.
- In in-game replays it currently sees the keys TUFReplay presses through the OS. That will stop when TUFReplay stops pressing keys through the OS; use `ReplayApi`.

Next: [3. Save values with each run](3-run-values.md)
