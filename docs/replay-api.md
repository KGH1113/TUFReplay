# Replay API

`TUFReplay.ReplayApi` is how overlay, key-viewer and other mods take part in TUFReplay's recordings and replays. It does two things:

- **State**: a mod saves its own state with each recorded run, typed by its own class, and gets it back when that run is replayed. An attempt counter can show the attempt number the run was played at; a key viewer can start from the key counts the player had then.
- **Replay**: while a run is replayed, the mod gets the replay's clock and recorded keys. The calls are the same whether TUFReplay plays the run in the game or renders it to video, so a mod writes one replay path.

`ApiVersion` is 1.

## Isolation rules

- **No reference to TUFReplay.dll.** Your mod finds `TUFReplay.ReplayApi` by name in the loaded assembly named `TUFReplay`.
- **Your own provider object.** You register an object of your own type. TUFReplay finds its public methods by name and binds them once.
- **Your own state type.** It stays in your assembly. TUFReplay reads its fields through reflection and never shares a type with you.
- **Only BCL and Unity types cross the boundary**: `string`, numbers, `bool`, `KeyCode`, `Canvas`, `RectInt`, `Action`. The method names and the provider id are the contract.

Either mod keeps working when the other is missing or turned off.

## TUFReplay surface

```csharp
namespace TUFReplay
{
    public static class ReplayApi
    {
        public const int ApiVersion = 1;
        public const int MaxStateBytes = 65536;     // largest saved state per run, UTF-8 JSON

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
    }
}
```

**`Register`**
- Registering an id again replaces its provider.
- It returns false when the id or the provider is invalid, and writes the reason to the mod log.
- The log line on success says what the provider takes part in, such as `Registered JipperResourcePack (replay begin/end, keys, clock, render canvases, frame capture).`

**The id**
- A provider that saves a state saves it under this id, so keep it stable.
- Use letters, digits, `.`, `_` and `-`, up to 64 characters, such as `"JipperResourcePack"` or `"Element.Attempts"`.

**Registering**
- Register from Unity's main thread.
- TUFReplay may load after your mod. If `TUFReplay.ReplayApi` is missing, try again when the next mod finishes loading.
- A mod can register one provider, or one per feature.

## Provider contract

TUFReplay looks up these public instance methods by name. Every method is optional, but a provider needs at least one. `TState` is your state class.

| Method | In-game replay | Render |
| --- | --- | --- |
| `TState CaptureRunState()` or `Task<TState> CaptureRunState()` | When a run starts recording | |
| `TState UpgradeRunState(TOldState old)` | When a run saved with an older state type is read; any number | |
| `void OnReplayBegin(TState recorded, bool render)`, or `(TState recorded)`, `(bool render)`, `()` | Before the level starts | Before the renderer opens the level |
| `void OnReplayClock(long ticks)` | Every frame, real time | Every frame, video time |
| `void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)` | Each recorded key | Each recorded key |
| `void OnReplayEnd()` | When the replay ends, fails or is replaced | When the render completes, fails or is cancelled |
| `IEnumerable<Canvas> GetReplayCanvases()` or `Canvas GetReplayCanvases()` | Not called | Once, as the video starts: the canvases to put in the video |
| `void OnBeforeFrameCapture()` / `void OnAfterFrameCapture()` | Not called | Around each new video frame |
| `void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)` | Not called | Once, after the last frame of a completed render: videos to lay over the finished video |

**During a replay**
- Take the replay's keys from `OnReplayKey` and ignore live input. TUFReplay still presses the keys through the OS during in-game replays, for now, so a mod that also listens to live input counts them twice.
- Take time from `OnReplayClock` / `ReplayTicks`, not from a stopwatch. In a render, the game runs slower than the video, so real time is wrong.
- Saving progress, counting attempts or adding to persistent counters during a replay is usually wrong: check `ReplayApi.IsReplaying`.

**`ticks`**
- 100 ns units (`TimeSpan.TicksPerSecond`), from 0 when the replay began. They never go back during a replay.
- In a render they are the video's time; in the game, real time since the replay began.
- A key's `ticks` is the time it was pressed, which can be slightly before the current clock.

**`nativeKey`**
- On Windows, the key's virtual-key code: the same code SkyHook reports there, so a mod that binds native codes matches it.
- Null on macOS, where SkyHook reports USB HID usages on newer game versions while recordings keep macOS key codes, and null in renders for now. Match on `key` then.

**Calls and failures**
- Every method runs on Unity's main thread. Per-frame calls allocate nothing on TUFReplay's side.
- An exception from your method is written to the log, at most three times per method for per-frame calls, and the replay or render goes on.

**What gets saved**
- Return null from `CaptureRunState` to save nothing for that run.
- `recorded` is null when the run has no readable state for your id: runs recorded before your mod saved one, runs where it saved nothing, and runs saved with a type you can't read anymore.

## Mods without ReplayApi

A mod that does not register gets nothing from TUFReplay:
- In renders it sees no replay keys. TUFReplay does not inject keys into SkyHook or Unity's Input.
- In in-game replays it currently sees the keys TUFReplay presses through the OS. That will stop when TUFReplay stops pressing keys through the OS; use `ReplayApi`.

## When methods are called

### Recording

**When `CaptureRunState` runs**
- When the run's input capture starts, at the countdown or at a checkpoint, before the first recorded input.
- If the activity database was busy then, when the player gains control instead.

**Which runs**
- Every attempt is its own run.
- Calibration runs, replays and offline renders are not recorded.

**Timing on the main thread**
- `CaptureRunState` runs just as a run starts. Keep it under a millisecond; a slower one is reported once in the log.
- For slow or out-of-process work, return a `Task<TState>`. It is saved if it finishes before the run is saved, when the player clears, dies or leaves the level. A later result is dropped with a log line.

**What is saved**
- The value is written to JSON as soon as TUFReplay gets it. Changing the object afterwards does not change what is saved.
- Runs TUFReplay does not save are dropped with their states: runs without forward progress and runs without native input.

### In-game replay

- `OnReplayBegin(recorded, render: false)` runs before the level starts, before ADOFAI's `scnGame.Play`, so an attempt counter can already tell that this play is a replay.
- `OnReplayClock` runs every frame; `OnReplayKey` delivers each recorded key at its time, whether or not the game window has focus.
- A timeline seek releases held keys and continues from the new time; keys in between are not sent.
- `OnReplayEnd` runs when the replay ends, fails, or is replaced by another replay. Held keys are released first.

### Render

TUFReplay replays the recording itself, and OrbitRender (RenderApi 3 or later) only captures and encodes it.
- `OnReplayBegin(recorded, render: true)` runs before TUFReplay hands the job to OrbitRender, before the level opens. Providers begin one after another, so another provider's state may not be restored yet.
- `GetReplayCanvases` runs once, after the level is ready and before the first frame. Canvases you create later are not in the video, so create them by then, or in this call. `RenderWidth`, `RenderHeight` and `RenderFps` hold the video's final values from this call on; before it they are the requested values.
- Every frame: `OnReplayClock` with the video time and `OnReplayKey` for the keys of that frame. `OnBeforeFrameCapture` and `OnAfterFrameCapture` wrap each new video frame; a repeated frame does not call them.
- `AddReplayOverlayVideos` runs once, after the last frame and before OrbitRender composes the finished video. See [Overlay videos](#overlay-videos).
- `OnReplayEnd` runs when the render completes, fails or is cancelled, after OrbitRender has restored the game and composed the video. A render that ends before its first frame, such as one cancelled while the level opens, calls `OnReplayEnd` without `GetReplayCanvases`.

`IsReplaying` is true from just before `OnReplayBegin` until just before `OnReplayEnd`, so it already reads false inside `OnReplayEnd`. Only one replay or render runs at a time.

### Overlay videos

A mod whose picture is slow to draw, such as a key viewer that runs in another app, can make its own video while the game renders and have OrbitRender lay it over the finished video, the way it adds the webcam. The game then never waits for that mod during the render.

**Whether the renderer takes them**
- `RenderTakesOverlayVideos` is true when the renderer will call `AddReplayOverlayVideos` (OrbitRender with `OverlayVideoVersion` 1 or later). Read it from `OnReplayBegin(…, render: true)` on.
- `RenderFfmpegPath` is the FFmpeg executable TUFReplay shares with OrbitRender, for writing your video. It is null outside a render and when FFmpeg is not installed.
- When either is missing, the renderer can't use your video: draw a canvas instead, or leave your picture out.

**`AddReplayOverlayVideos(add)`**
- Runs once on Unity's main thread, after the last frame, before OrbitRender composes the video. It may block until your videos are written; the render waits.
- It is not called when the render fails or is cancelled. Clean up in `OnReplayEnd`, which runs after the video is composed.
- Call `add(path, source, target)` once per video, during the call. Videos added later are left out.
  - `path`: an absolute path to a video FFmpeg can read. Use a format with alpha, such as QuickTime Animation (`-c:v qtrle -pix_fmt argb`), for a picture that is not a full rectangle.
  - `source`: the part of each of the video's frames to use, in its pixels.
  - `target`: where that part goes in the rendered video, in pixels of `RenderWidth` × `RenderHeight`. A target of another size scales the part; a target partly outside the video is cut off.
  - Both rectangles count from the top left, with `y` going down.
- The video's time 0 is the rendered video's first frame. Make it at `RenderFps`, one frame per video frame. A video that ends early holds its last frame.
- Videos are drawn in the order added, over the game and its canvases and under the webcam.
- A video whose file is missing, whose path is not absolute, or whose rectangle is empty is left out with a log line.

## State types

A state type is a class with a parameterless constructor. The constructor can be private.

**Saved members**
- Public instance fields.
- Public properties with a getter and any setter.

**Skipped members**
- Get-only properties, such as computed ones.
- Fields marked `[NonSerialized]`.
- A `readonly` field is an error. Remove `readonly`, or mark the field `[NonSerialized]`.

Member types can be:

| Kind | Types |
| --- | --- |
| Values | `bool`, `int`, `long`, `float`, `double`, `string`, an enum, `DateTime`, `byte[]` |
| Nullable values | `int?`, `Hand?` and so on. Reference types can always be null. |
| Collections | `T[]`, `List<T>`, `Dictionary<string, T>` |
| Nested | A class or struct made of the above |

**Not storable**
- `object`, interfaces and abstract classes
- Other collections
- Other number types (`short`, `uint`, `decimal`, …)
- `[Flags]` enums. Store a list of the enum instead.
- Unity objects
- Types that contain themselves

**Values**
- Floats must be finite. Use a nullable number for "no value".
- Enums are saved by member name.
- A `DateTime` is saved as UTC.
- A nested object must be exactly its declared type.
- A saved state can be up to 64 KiB.

**Reading back**
- A list or dictionary your constructor already creates is refilled rather than replaced, so a custom key comparer is kept.

**When TUFReplay checks**
- TUFReplay checks the type when you register and reports the first member it can't store, with how to fix it.
- It checks values when they are saved.

## Changing a state type

Every run keeps the exact shape its state was saved with. When your type changes, TUFReplay reads older runs this way.

**Read automatically**
- An added member keeps the value your constructor gives it.
- A removed member is skipped.
- A number widens: `int` to `long` or `double`, `float` to `double`.
- A value becomes nullable.
- An enum gains members.

**Needs `UpgradeRunState`**
- A changed type, such as `string` to `double`, `long` to `int`, or `int?` to `int`.
- A removed enum member.
- Keep the old class with the members it had and add a method that converts it. TUFReplay uses the upgrade whose old type matches the saved run.

```csharp
// Saved by earlier versions of the mod.
internal sealed class AttemptStateV1
{
    public int Attempt;
    public string Speed;   // "1.5x"
}

internal sealed class AttemptState
{
    public int Attempt;
    public double Speed = 1;
}

// On the provider:
public AttemptState UpgradeRunState(AttemptStateV1 old) =>
    new AttemptState { Attempt = old.Attempt, Speed = double.Parse(old.Speed.TrimEnd('x'), CultureInfo.InvariantCulture) };
```

**Renaming a member**
- A rename looks like one removed member and one added member, so it would read without error and lose the value.
- An upgrade whose old type matches the saved run exactly is preferred over the automatic reading. Add one for the old class to keep the value.

**Checks at registration**
- When you register, TUFReplay checks every shape your id was saved with.
- It writes a log line for runs it can't read without an upgrade, and for runs that look renamed.

## Example adapter

A key viewer that shows replay keys and puts its canvas in videos, without saving a state:

```csharp
// Optional bridge to TUFReplay's ReplayApi. No compile-time reference to TUFReplay.
internal static class TUFReplayAPI
{
    private const string ProviderId = "MyKeyViewer";
    private const int RequiredApiVersion = 1;
    private static Func<string, object, bool> _register;
    private static Action<string> _unregister;
    private static bool _registered;

    public static void TryRegister()
    {
        if (_registered || !Resolve()) return;
        _registered = _register(ProviderId, new Provider());
    }

    public static void Shutdown()
    {
        _unregister?.Invoke(ProviderId);
        _registered = false;
    }

    private static bool Resolve()
    {
        if (_register != null) return true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "TUFReplay") continue;
            Type api = assembly.GetType("TUFReplay.ReplayApi", false);
            if (api?.GetField("ApiVersion")?.GetValue(null) is not int version || version < RequiredApiVersion) continue;
            _register = (Func<string, object, bool>)Delegate.CreateDelegate(
                typeof(Func<string, object, bool>), api.GetMethod("Register"));
            _unregister = (Action<string>)Delegate.CreateDelegate(
                typeof(Action<string>), api.GetMethod("Unregister"));
            return true;
        }
        return false;
    }

    // TUFReplay finds these methods by name.
    private sealed class Provider
    {
        public void OnReplayBegin(bool render) => KeyViewer.Instance.BeginReplay();      // stop live input, copy the counts
        public void OnReplayEnd() => KeyViewer.Instance.EndReplay();                   // back to live input
        public void OnReplayClock(long ticks) => KeyViewer.Instance.ReplayTicks = ticks;
        public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks) =>
            KeyViewer.Instance.ApplyReplayKey(key, nativeKey, down, ticks);
        public IEnumerable<Canvas> GetReplayCanvases()
        {
            yield return KeyViewer.Instance.Canvas;
        }
    }
}
```

Register when your mod is enabled, and again whenever another mod finishes loading, until it succeeds:

```csharp
protected override void OnEnable() => TUFReplayAPI.TryRegister();
protected override void OnDisable() => TUFReplayAPI.Shutdown();

[JAPatch(typeof(UnityModManager.ModEntry), "Load", PatchType.Postfix, false)]
private static void OnModLoaded() => TUFReplayAPI.TryRegister();
```

A provider that also saves a state adds `CaptureRunState` and takes it in `OnReplayBegin`:

```csharp
internal sealed class AttemptState
{
    public int Attempt;
    public int FullAttempt;
}

internal sealed class AttemptStore
{
    public AttemptState CaptureRunState() =>
        new AttemptState { Attempt = Attempts.Current, FullAttempt = Attempts.Full };

    public void OnReplayBegin(AttemptState recorded, bool render) => AttemptText.ShowRecorded(recorded); // null: "-"
    public void OnReplayEnd() => AttemptText.ShowLive();
}
```

## Storage

States live in TUFReplay's activity database.

**Tables**
- `run_states` has one row per run and state id: the shape id, the value as JSON, and when it was saved.
- `run_state_shapes` keeps each distinct shape once, keyed by the SHA-256 of its canonical JSON.

**Saving and deleting**
- A run's states are written in the same transaction as the run.
- They are deleted with the run, because the foreign key cascades.
- A locked run keeps its states.

**Compatibility with older TUFReplay**
- The tables are added in place, and the database version stays 1. Older TUFReplay builds still open the file.
- Their run deletes cascade too, because the foreign key lives in `run_states`.

## Log messages

Every message starts with `[ReplayApi]` and names the provider id.

```text
[ReplayApi] Registered JipperResourcePack (replay begin/end, keys, clock, render canvases, frame capture).
[ReplayApi] MyMod was not registered: Provider.OnReplayClock must be void OnReplayClock(long ticks).
[ReplayApi] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[ReplayApi] Element.Attempts: 12 saved runs can't be read: AttemptState.Speed was string and is now double. Add UpgradeRunState(the old type) to AttemptStore, or those runs reach OnReplayBegin as null.
[ReplayApi] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[ReplayApi] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
[ReplayApi] Element.Render: overlay video /tmp/render/hand.mov was left out: the file does not exist.
```
