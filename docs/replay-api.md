# Run state API

Other mods can save their own state with each run TUFReplay records and get it back when TUFReplay replays that run in the game or renders it through OrbitRender. An attempt counter can show the attempt number the run was played at, and a key viewer can start from the key counts the player had then.

The API is in TUFReplay 1.x builds that have `TUFReplay.RunStateApi`, with `ApiVersion = 1`.

## Isolation rules

The rules are the same as for OrbitRender's Overlay API.

- **No reference to TUFReplay.dll.** Your mod finds `TUFReplay.RunStateApi` by name in the loaded assembly named `TUFReplay`.
- **Your own provider object.** You register an object of your own type. TUFReplay finds its methods by name and calls them through reflection.
- **Your own state type.** It stays in your assembly. TUFReplay reads its fields through reflection and never shares a type with you.
- **Only BCL types cross the boundary.** These are `string`, `bool` and `object`. The method names and the state id are the contract.

So either mod keeps working when the other is missing or turned off.

## TUFReplay surface

```csharp
namespace TUFReplay
{
    public static class RunStateApi
    {
        public const int ApiVersion = 1;            // changes only when a member or method changes meaning
        public const int MaxStateBytes = 65536;     // largest saved state per run, UTF-8 JSON

        public static bool IsReplaying { get; }     // between OnReplayBegin and OnReplayEnd
        public static bool IsRendering { get; }     // the replay is an OrbitRender render
        public static string ReplayRunId { get; }   // the run being replayed, or null

        public static bool Register(string id, object provider);
        public static void Unregister(string id);
        public static bool IsRegistered(string id);
    }
}
```

**`Register`**
- Registering an id again replaces its provider.
- It returns false when the id or the provider is invalid, and writes the reason to the mod log.

**The id**
- Saved with every run, so keep it stable.
- Use letters, digits, `.`, `_` and `-`, up to 64 characters. Name it after your mod and the state, such as `"Element.Attempts"`.

**Registering**
- Register from Unity's main thread.
- TUFReplay may load after your mod. If `TUFReplay.RunStateApi` is missing, try again when the next mod finishes loading.

## Provider contract

TUFReplay looks up these public instance methods by name. `TState` is your state class.

| Method | Called |
| --- | --- |
| `TState CaptureRunState()` or `Task<TState> CaptureRunState()` | When a run starts recording |
| `void OnReplayBegin(TState recorded, bool render)` or `void OnReplayBegin(TState recorded)` | Before TUFReplay replays a run in the game or renders it |
| `void OnReplayEnd()` | When that replay or render ends |
| `TState UpgradeRunState(TOldState old)` | When a run saved with an older state type is read. A provider can have any number of these. |

**Which methods**
- Every method is optional, but a provider needs `CaptureRunState` or `OnReplayBegin`.
- `CaptureRunState` and `OnReplayBegin` must use the same `TState`.

**Calls and failures**
- TUFReplay calls every method on Unity's main thread.
- An exception from your method is written to the log, and recording, replay and rendering go on.

**What gets saved**
- Return null from `CaptureRunState` to save nothing for that run.
- `recorded` is null when the run has no readable state for your id. That covers runs recorded before your mod used the API, runs where your mod saved nothing, and runs saved with a type you can't read anymore.

## When methods are called

### Recording

**When `CaptureRunState` runs**
- It runs when the run's input capture starts, at the countdown or at a checkpoint, before the first recorded input.
- If the activity database was busy then, it runs when the player gains control instead.

**Which runs**
- Every attempt is its own run.
- Calibration runs, replays and offline renders are not recorded.

**Timing on the main thread**
- `CaptureRunState` runs on the main thread just as a run starts. Keep it under a millisecond.
- A slower one is reported once in the log.

**Slow or out-of-process work**
- Return a `Task<TState>`. It is saved if it finishes before the run is saved, which happens when the player clears, dies or leaves the level.
- A result that arrives later is dropped with a log line.

**What is saved**
- The value is written to JSON as soon as TUFReplay gets it. Changing the object afterwards does not change what is saved.
- Runs TUFReplay does not save are dropped with their states. That covers runs without forward progress and runs without native input.

### In-game replay

- `OnReplayBegin(recorded, render: false)` runs before the level starts.
  - It runs before ADOFAI's `scnGame.Play`, so an attempt counter can already tell that this play is a replay.
- `OnReplayEnd` runs when the replay ends, fails, or is replaced by another replay.
- A timeline seek inside the replay restarts the level without calling these again.

### OrbitRender render

- `OnReplayBegin(recorded, render: true)` runs before TUFReplay hands the render job to OrbitRender. That is before OrbitRender's Overlay API calls `OnRenderSessionBegin`.
- `OnReplayEnd` runs when the render completes, fails or is cancelled.

`IsReplaying` is true from just before `OnReplayBegin` until just before `OnReplayEnd`, so it already reads false inside `OnReplayEnd`. Only one replay or render runs at a time.

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

Element keeps its attempt numbers and ElementNote's key counts with each run. ElementNote runs in another process, so its counts arrive through a `Task`.

```csharp
// Element/OtherModApi/TUFReplayRunStateAPI.cs
// Optional bridge to TUFReplay's run state API. No compile-time reference to TUFReplay.
internal static class TUFReplayRunStateAPI
{
    public const int RequiredApiVersion = 1;
    private static Func<string, object, bool> _register;
    private static Action<string> _unregister;
    private static bool _registered;

    public static void TryRegister()
    {
        if (_registered || !Resolve()) return;
        _registered = _register("Element.Attempts", new AttemptStore())
                    & _register("Element.KeyCounts", new KeyCountStore());
    }

    public static void Shutdown()
    {
        _unregister?.Invoke("Element.Attempts");
        _unregister?.Invoke("Element.KeyCounts");
        _registered = false;
    }

    private static bool Resolve()
    {
        if (_register != null) return true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "TUFReplay") continue;
            Type api = assembly.GetType("TUFReplay.RunStateApi", false);
            if (api?.GetField("ApiVersion")?.GetValue(null) is not int version || version < RequiredApiVersion) continue;
            _register = (Func<string, object, bool>)Delegate.CreateDelegate(
                typeof(Func<string, object, bool>), api.GetMethod("Register"));
            _unregister = (Action<string>)Delegate.CreateDelegate(
                typeof(Action<string>), api.GetMethod("Unregister"));
            return true;
        }
        return false;
    }
}

internal sealed class AttemptState
{
    public int Attempt;
    public int FullAttempt;
}

internal sealed class KeyCountState
{
    // ElementNote tab → key slot → presses
    public Dictionary<string, Dictionary<string, int>> ByTab = new();
}

internal sealed class AttemptStore
{
    public AttemptState CaptureRunState()
    {
        AttemptCounts now = AttemptTracker.Instance.Current;   // already counts this play
        return new AttemptState { Attempt = now.Attempt, FullAttempt = now.FullAttempt };
    }

    public void OnReplayBegin(AttemptState recorded, bool render) => AttemptDisplay.ShowRecorded(recorded); // null: "-"

    public void OnReplayEnd() => AttemptDisplay.ShowLive();
}

internal sealed class KeyCountStore
{
    public async Task<KeyCountState> CaptureRunState() =>
        new KeyCountState { ByTab = await ElementNoteClient.Instance.ReadCountersAsync() };

    public void OnReplayBegin(KeyCountState recorded, bool render)
    {
        if (render) KeyCountBaseline.Set(recorded?.ByTab);            // used by the OrbitRender render
        else ElementNoteClient.Instance.BeginReplay(recorded?.ByTab); // replayed keys don't add to the totals
    }

    public void OnReplayEnd()
    {
        KeyCountBaseline.Clear();
        ElementNoteClient.Instance.EndReplay();
    }
}
```

Register like any optional integration: when your mod is enabled, and again whenever another mod finishes loading, until it succeeds.

```csharp
protected override void OnEnable() => TUFReplayRunStateAPI.TryRegister();
protected override void OnDisable() => TUFReplayRunStateAPI.Shutdown();

[JAPatch(typeof(UnityModManager.ModEntry), "Load", PatchType.Postfix, false)]
private static void OnModLoaded() => TUFReplayRunStateAPI.TryRegister();
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

Every message starts with `[RunState]` and names the state id.

```text
[RunState] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[RunState] Element.Attempts: 12 saved runs can't be read: AttemptState.Speed was string and is now double. Add UpgradeRunState(the old type) to AttemptStore, or those runs reach OnReplayBegin as null.
[RunState] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[RunState] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
```
