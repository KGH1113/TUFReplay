# Reference

## TUFReplay surface

```csharp
namespace TUFReplay
{
    public static class ReplayApi
    {
        public const int ApiVersion = 2;
        public const int MaxStateBytes = 65536;     // largest saved start value or event, UTF-8 JSON

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

        public static ReplayEventStore GetEventStore(string id);    // the id's place in each recorded run
    }

    public sealed class ReplayEventStore
    {
        public string Id { get; }
        public bool AddEvent(object e);                              // an event at this moment of the run
    }
}
```

## Provider contract

TUFReplay looks up these public instance methods by name. Every method is optional, but a provider needs at least one. `TState` is the id's state type, and `TEvent` one of its event types.

| Method | In-game replay | Render | Step |
| --- | --- | --- | --- |
| `TState CaptureRunState()` or `Task<TState> CaptureRunState()` | When a run starts recording: the run's start value | | [3](3-run-values.md) |
| `TState UpgradeRunState(TOldState old)` | When a start value saved with an older state type is read; any number | | [4](4-state-types.md) |
| `TEvent UpgradeRunEvent(TOldEvent old)` | When an event saved with an older event type is read; any number | | [4](4-state-types.md) |
| `void OnReplayBegin(bool render)` or `()` | Before the level starts | Before the renderer opens the level | [2](2-follow-a-replay.md) |
| `void OnReplayState(TState state, long ticks)` | Right after `OnReplayBegin` with the start value, and again after each seek | Right after `OnReplayBegin` with the start value | [3](3-run-values.md) |
| `void OnReplayEvent(TEvent e, long ticks)`, one for each event type | Each event at its time; after a seek, every event up to there at once | Each event at its video time | [3](3-run-values.md) |
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
- `run_events` has one row per event added during a run: the run, id, order, time on the run's timeline, the event type's namespace and name, shape id and value as JSON.
- `run_state_shapes` keeps each distinct shape once, keyed by the SHA-256 of its canonical JSON.

**Saving and deleting**
- A run's start values and events are written in the same transaction as the run.
- They are deleted with the run, because the foreign key cascades.
- A locked run keeps its values.

**Compatibility with older TUFReplay**
- The tables are added in place, and the database version stays 1. Older TUFReplay builds still open the file and read the start values.
- Their run deletes cascade too, because the foreign keys live in `run_states` and `run_events`.

## Changes in ApiVersion 2

- `OnReplayBegin` no longer takes the state: it is `void OnReplayBegin(bool render)` or `void OnReplayBegin()`. A provider whose `OnReplayBegin` takes a state is not registered. Take the start value in `OnReplayState(TState state, long ticks)`, which runs right after it.
- Events: `GetEventStore(id).AddEvent(e)` adds them during a run, the `OnReplayEvent(TEvent e, long ticks)` overloads hand them back at their times, and `UpgradeRunEvent` reads older event types.
- `CaptureRunState`, `UpgradeRunState` and the other methods are unchanged. A mod that needs `OnReplayState` or events requires `ApiVersion` 2.

## Log messages

Every message starts with `[ReplayApi]` and names the id.

```text
[ReplayApi] Registered MyOverlay (state OverlayState, replay begin/end, replay state, events LayoutChanged/ChatReceived, keys, clock).
[ReplayApi] MyMod was not registered: Provider.OnReplayClock must be void OnReplayClock(long ticks).
[ReplayApi] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[ReplayApi] MyMod was not registered: Provider.OnReplayBegin must be void OnReplayBegin() or void OnReplayBegin(bool render). The run's state no longer comes with it: take it in void OnReplayState(AttemptState state, long ticks), which runs right after it.
[ReplayApi] MyMod was not registered: Provider.OnReplayEvent(AttemptState e, long ticks) no longer gets the run's start value. Take it in void OnReplayState(AttemptState state, long ticks), which runs right after OnReplayBegin and after each seek.
[ReplayApi] MyOverlay: 12 saved runs can't be read: OverlayState.Speed was string and is now double. Add UpgradeRunState(the old type) to ReplayFollower, or those runs' start values reach OnReplayState as null.
[ReplayApi] MyOverlay: LayoutChanged events in 3 saved runs can't be read: LayoutChanged.Size was string and is now float. Add UpgradeRunEvent(the old type) to ReplayFollower, or replays skip those events.
[ReplayApi] MyOverlay: saved runs have HeartRateChanged events, but ReplayFollower has no OnReplayEvent(HeartRateChanged e, long ticks), so replays skip them.
[ReplayApi] MyOverlay: AddEvent got LayoutSettings, but ReplayFollower has no OnReplayEvent(LayoutSettings e, long ticks). It takes LayoutChanged, ChatReceived.
[ReplayApi] MyOverlay: this run already has 1000 events, the most one run keeps (1000 events or 1024 KiB). Later events are not saved. Add an event when something changes, not every frame.
[ReplayApi] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[ReplayApi] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
[ReplayApi] Element.Render: overlay video /tmp/render/hand.mov was left out: the file does not exist.
```
