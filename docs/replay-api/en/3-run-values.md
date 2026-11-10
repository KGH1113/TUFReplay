# 3. Save values with each run

TUFReplay saves your mod's values with each recorded run and hands them back when that run is replayed. An attempt counter shows the attempt the run was played at. A key viewer shows the layout the player had at each moment, even if they changed it mid-run. A heart-rate overlay shows the readings from that run.

Keys and judgments need nothing here: the replay plays them again. Save what changes for other reasons.

## One id per value

Think of an id as the name a value is saved under. Each id has one class, its state type, and holds one value at a time: the value the run starts with, then each new one recorded during the run.

| Id | State type | Saved |
| --- | --- | --- |
| `MyOverlay.Attempts` | `AttemptState` | As the run starts |
| `MyOverlay.Layout` | `LayoutSettings` | As the run starts, and each time the player changes it |
| `MyOverlay.HeartRate` | `HeartRate` | Each reading during the run |

Values of different kinds go under different ids, so each keeps its own type. In the adapter from step 1, register one small provider per id, and unregister each in `Shutdown`:

```csharp
public static void TryRegister()
{
    if (_registered || !Resolve()) return;
    _registered = _register("MyOverlay", new ReplayFollower());   // step 2
    _register("MyOverlay.Attempts", new AttemptStore());
    _register("MyOverlay.Layout", new LayoutStore());
    _register("MyOverlay.HeartRate", new HeartRateStore());
}
```

## The value a run starts with

`TState CaptureRunState()` returns the value to save as the run starts.

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

    public void OnReplayEvent(AttemptState value, long ticks) => AttemptText.ShowRecorded(value); // null: "-"
    public void OnReplayEnd() => AttemptText.ShowLive();
}
```

**When it runs**
- When the run's input capture starts, at the countdown or at a checkpoint, before the first recorded input.
- If the activity database was busy then, when the player gains control instead.
- Return null to save no start value for that run.

**Keep it fast**
- It runs on the main thread just as a run starts. Keep it under a millisecond; a slower one is reported once in the log.
- For slow or out-of-process work, return a `Task<TState>`. It is saved if it finishes before the run is saved, when the player clears, dies or leaves the level. A later result is dropped with a log line.

The value is written to JSON as soon as TUFReplay gets it. Changing the object afterwards does not change what is saved.

## Values that change during the run

Call `ReplayApi.RecordRunEvent(id, value)` when the value changes.

```csharp
internal sealed class LayoutSettings
{
    public float Size;
    public List<KeyCode> Keys = new List<KeyCode>();
}

internal sealed class LayoutStore
{
    public LayoutSettings CaptureRunState() => Layout.Snapshot();

    public void OnReplayEvent(LayoutSettings value, long ticks) => Layout.ShowRecorded(value); // null: the live layout
    public void OnReplayEnd() => Layout.ShowLive();
}

// When the player closes the settings window:
TUFReplayAPI.Record("MyOverlay.Layout", Layout.Snapshot());
```

A value that has nothing to save as the run starts leaves out `CaptureRunState`. The state type then comes from `OnReplayEvent`:

```csharp
internal sealed class HeartRate
{
    public int Bpm;
}

internal sealed class HeartRateStore
{
    public void OnReplayEvent(HeartRate value, long ticks) => HeartRateText.Show(value?.Bpm); // null: no reading yet
    public void OnReplayEnd() => HeartRateText.ShowLive();
}

// When the sensor sends a reading:
TUFReplayAPI.Record("MyOverlay.HeartRate", new HeartRate { Bpm = bpm });
```

**What it saves**
- `value` must be exactly the state type of the id.
- TUFReplay stamps it with this moment on the run's timeline, the same clock as the run's keys and judgments, and writes it to JSON now. Changing the object afterwards does not change what is saved.
- Values recorded at the same moment keep only the last one: several in one frame, or all of them while the game is paused, since the song does not move then.
- One run keeps up to 1,000 values per id, and 1 MiB of them. Record a value when it changes, not every frame.

**When it records**
- From when `CaptureRunState` would run until the run is saved: after a clear, and after a death once the player retries or returns to the editor, so values on the fail screen count too.
- Call it on Unity's main thread. It returns false when no run is recording, which is normal: outside a level, in calibration runs, in replays and renders. Other failures, such as a value of the wrong type, return false and are written to the log once.

**Things where every one counts**
- An id holds one value at a time, so recording chat messages one by one keeps only the last of those at the same moment, and a seek hands back only the last one.
- Record what your mod shows instead, such as the last ten messages.

## Getting them back

`void OnReplayEvent(TState value, long ticks)` hands the values back during a replay.

- As the replay begins, right after the provider's `OnReplayBegin` if it has one, it gets the run's start value at `ticks` 0, or null. When the run recorded no values under the id, that is the only call.
- Then it gets each value recorded with `RecordRunEvent`, at the time it was recorded, before that frame's keys. Like a key, its `ticks` can be slightly before the current clock.
- After a timeline seek, it gets the value in effect at the new time, if that is a different one from the last it got: the last value recorded before that time, or the start value.
- Each call hands over a new object, so you can keep it.

**When it gets null**
- The run was recorded before your mod saved a value under this id.
- Your mod saved nothing for that run.
- The run was saved with a type you can't read anymore. See [step 4](4-state-types.md#changing-a-state-type).

A recorded value that can't be read is skipped and reported once in the log.

**Several ids**
- Providers begin one after another, in the order they were registered. Each gets `OnReplayBegin` and then its start value before the next one begins.
- If one provider needs another's value when it begins, register the other first.

## Which runs

- Every attempt is its own run.
- Calibration runs, replays and offline renders are not recorded.
- Runs TUFReplay does not save are dropped with their values: runs without forward progress and runs without native input.

Next: [4. State types](4-state-types.md)
