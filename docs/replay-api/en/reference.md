# Reference

## TUFReplay surface

```csharp
namespace TUFReplay
{
    public static class ReplayApi
    {
        public const int ApiVersion = 2;
        public const int MaxStateBytes = 65536;     // largest saved value, UTF-8 JSON

        public static bool IsReplaying { get; }     // between OnReplayBegin and OnReplayEnd
        public static bool IsRendering { get; }     // the replay is being rendered to video
        public static string ReplayRunId { get; }   // the run being replayed, or null
        public static long ReplayTicks { get; }     // the replay clock, 100 ns ticks
        public static int RenderWidth { get; }      // the video while rendering, otherwise 0
        public static int RenderHeight { get; }
        public static int RenderFps { get; }
        public static bool RenderTakesOverlayVideos { get; } // the renderer lays your videos over the video
        public static string RenderFfmpegPath { get; }       // FFmpeg shared with the renderer, or null

        public static bool Register(string id, object provider);
        public static void Unregister(string id);
        public static bool IsRegistered(string id);

        public static bool RecordRunEvent(string id, object value);  // a value at this moment of the run
    }
}
```

## Provider contract

TUFReplay looks up these public instance methods by name. Every method is optional, but a provider needs at least one. `TState` is the id's state type.

| Method | In-game replay | Render | Step |
| --- | --- | --- | --- |
| `TState CaptureRunState()` or `Task<TState> CaptureRunState()` | When a run starts recording: the run's start value | | [3](3-run-values.md) |
| `TState UpgradeRunState(TOldState old)` | When a value saved with an older state type is read; any number | | [4](4-state-types.md) |
| `void OnReplayBegin(bool render)` or `()` | Before the level starts | Before the renderer opens the level | [2](2-follow-a-replay.md) |
| `void OnReplayEvent(TState value, long ticks)` | Right after `OnReplayBegin` with the start value, then each recorded value at its time, and after a seek | The same, at video time | [3](3-run-values.md) |
| `void OnReplayClock(long ticks)` | Every frame, real time | Every frame, video time | [2](2-follow-a-replay.md) |
| `void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)` | Each recorded key | Each recorded key | [2](2-follow-a-replay.md) |
| `void OnReplayEnd()` | When the replay ends, fails or is replaced | When the render completes, fails or is cancelled | [2](2-follow-a-replay.md) |
| `IEnumerable<Canvas> GetReplayCanvases()` or `Canvas GetReplayCanvases()` | Not called | Once, as the video starts: the canvases to put in the video | [5](5-render.md) |
| `void OnBeforeFrameCapture()` / `void OnAfterFrameCapture()` | Not called | Around each new video frame | [5](5-render.md) |
| `void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)` | Not called | Once, after the last frame of a completed render: videos to lay over the finished video | [5](5-render.md) |

## Storage

Values live in TUFReplay's activity database.

**Tables**
- `run_states` has one row per run and id for the start value: the shape id, the value as JSON, and when it was saved.
- `run_state_events` has one row per value recorded during a run: the run, id, order, time on the run's timeline, shape id and value as JSON.
- `run_state_shapes` keeps each distinct shape once, keyed by the SHA-256 of its canonical JSON.

**Saving and deleting**
- A run's start values and recorded values are written in the same transaction as the run.
- They are deleted with the run, because the foreign key cascades.
- A locked run keeps its values.

**Compatibility with older TUFReplay**
- The tables are added in place, and the database version stays 1. Older TUFReplay builds still open the file and read the start values.
- Their run deletes cascade too, because the foreign keys live in `run_states` and `run_state_events`.

## Changes in ApiVersion 2

- `OnReplayBegin` no longer takes the state: it is `void OnReplayBegin(bool render)` or `void OnReplayBegin()`. A provider whose `OnReplayBegin` takes a state is not registered. Take the start value in `OnReplayEvent(TState value, long ticks)`, which runs right after it.
- `RecordRunEvent` records values during a run, and `OnReplayEvent` hands them back at their times.
- `CaptureRunState`, `UpgradeRunState` and the other methods are unchanged. A mod that needs `OnReplayEvent` requires `ApiVersion` 2.

## Log messages

Every message starts with `[ReplayApi]` and names the id.

```text
[ReplayApi] Registered JipperResourcePack (replay begin/end, keys, clock, render canvases, frame capture).
[ReplayApi] MyMod was not registered: Provider.OnReplayClock must be void OnReplayClock(long ticks).
[ReplayApi] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[ReplayApi] Element.Attempts: 12 saved runs can't be read: AttemptState.Speed was string and is now double. Add UpgradeRunState(the old type) to AttemptStore, or those runs' values reach OnReplayEvent as null or are skipped.
[ReplayApi] MyMod was not registered: Provider.OnReplayBegin must be void OnReplayBegin() or void OnReplayBegin(bool render). The run's state no longer comes with it: take it in void OnReplayEvent(AttemptState value, long ticks), which runs right after it.
[ReplayApi] MyKeyViewer.Layout: RecordRunEvent got AttemptState, but the state type is LayoutSettings.
[ReplayApi] MyKeyViewer.Layout: this run already has 1000 recorded values, the most one run keeps (1000 values or 1024 KiB). Later values are not saved. Record a value when it changes, not every frame.
[ReplayApi] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[ReplayApi] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
[ReplayApi] Element.Render: overlay video /tmp/render/hand.mov was left out: the file does not exist.
```
