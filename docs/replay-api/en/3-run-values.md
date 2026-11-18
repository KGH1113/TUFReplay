# 3. Save values with each run

TUFReplay saves your mod's values with each recorded run and hands them back when that run is replayed. An attempt counter shows the attempt the run was played at. A key viewer shows the layout the player had at each moment, even if they changed it mid-run. A chat overlay shows the messages as they arrived during that run.

Keys and judgments need nothing here: the replay plays them again. Save what changes for other reasons.

## A start value and events

Your id gets its own place in each recorded run. It holds two things:

- **The start value**: one object with what your mod shows as the run starts. Its class is your state type.
- **Events**: what happened during the run, at the moment it happened. Each kind of event is its own class, such as `LayoutChanged` or `ChatReceived`.

The replay hands them back the same way: the start value first, then each event at its time. Your mod applies each event to what it shows, as it does live.

| | While a run records | While it is replayed |
| --- | --- | --- |
| Start value | `TState CaptureRunState()` | `void OnReplayState(TState state, long ticks)` |
| Events | `AddEvent(e)` on your event store | `void OnReplayEvent(TEvent e, long ticks)`, one for each event type |

```csharp
internal sealed class OverlayState
{
    public int Attempt;
    public LayoutSettings Layout;
    public List<ChatMessage> Chat = new List<ChatMessage>();
}

internal sealed class LayoutChanged { public LayoutSettings Layout; }
internal sealed class ChatReceived { public ChatMessage Message; }
internal sealed class HeartRateChanged { public int Bpm; }

internal sealed class ReplayFollower
{
    // … the methods from step 2 …

    public OverlayState CaptureRunState() => new OverlayState
    {
        Attempt = Attempts.Current,
        Layout = Layout.Snapshot(),
        Chat = Chat.Recent(50),
    };

    public void OnReplayState(OverlayState state, long ticks) => Overlay.Show(state); // null: nothing was saved
    public void OnReplayEvent(LayoutChanged e, long ticks) => Overlay.SetLayout(e.Layout);
    public void OnReplayEvent(ChatReceived e, long ticks) => Overlay.AddChat(e.Message);
    public void OnReplayEvent(HeartRateChanged e, long ticks) => Overlay.SetHeartRate(e.Bpm);
}

// While playing, with the adapter from step 1:
if (TUFReplayAPI.IsRecording())   // the player closed the settings window; no copy unless a run records
    TUFReplayAPI.AddEvent(new LayoutChanged { Layout = Layout.Snapshot() });
TUFReplayAPI.AddEvent(new ChatReceived { Message = message });   // a chat message arrived
```

## The start value

`TState CaptureRunState()` returns the value to save as the run starts.

**When it runs**
- When the run's input capture starts, at the countdown or at a checkpoint, before the first recorded input.
- If the activity database was busy then, when the player gains control instead.
- Return null to save no start value for that run.
- A provider with nothing to save as the run starts leaves it out. Its state type then comes from `OnReplayState`, which gets null.

**Keep it fast**
- It runs on the main thread just as a run starts. Keep it under a millisecond; a slower one is reported once in the log.
- For slow or out-of-process work, return a `Task<TState>`. It is saved if it finishes before the run is saved, when the player clears, dies or leaves the level. A later result is dropped with a log line.

The value is written to JSON as soon as TUFReplay gets it. Changing the object afterwards does not change what is saved.

## Events

`ReplayApi.GetEventStore(id)` returns your id's event store. Its `AddEvent(object e)` adds an event to the run recording now. The adapter from step 1 binds it once.

**Event types**
- Each event type is a class with an `OnReplayEvent(TEvent e, long ticks)` overload on the provider. The overloads tell TUFReplay which types the id takes.
- `AddEvent` takes exactly those types. Any other type returns false and is written to the log.
- Each type follows the same rules as a state type. See [step 4](4-state-types.md).

**What it saves**
- TUFReplay stamps the event with this moment on the run's timeline, the same clock as the run's keys and judgments, and writes it to JSON now. Changing the object afterwards does not change what is saved.
- Every event is kept, several at one moment too, such as chat messages while the game is paused.
- One run keeps up to 1,000 events per id, and 1 MiB of them. Add an event when something changes, not every frame.

**When it records**
- From when `CaptureRunState` would run until the run is saved: after a clear, and after a death once the player retries or returns to the editor, so events on the fail screen count too.
- Call it on Unity's main thread. It returns false when no run is recording, which is normal: outside a level, in calibration runs, in replays and renders. Other failures, such as an event of a type the provider does not take, return false and are written to the log once.
- `ReplayApi.IsRecording` is true for exactly this span. Check it before building an event that takes work to make, such as a copy of the layout.

## Getting them back

- As the replay begins, right after the provider's `OnReplayBegin` if it has one, `OnReplayState` gets the run's start value at `ticks` 0, or null.
- Then each event reaches the `OnReplayEvent` overload for its type, at the time it was added, before that frame's keys. Like a key, its `ticks` can be slightly before the current clock.
- Each call hands over a new object, so you can keep it.

**After a timeline seek**
- The replay starts over from the run's start: `OnReplayState` gets the start value again, then every event up to the new time follows at once, in order.
- They all get the same `ticks` as that `OnReplayState`. Rebuild what you show from them, and skip one-off effects, such as a sound for a new message, when an event's `ticks` equal the last `OnReplayState`'s.
- That is why a provider with `OnReplayEvent` needs `OnReplayState`: it is how your mod knows to start over.

**When `OnReplayState` gets null**
- The run was recorded before your mod saved a start value under this id.
- Your mod saved nothing for that run.
- The run was saved with a type you can't read anymore. See [step 4](4-state-types.md#changing-a-type).

An event that can't be read is skipped and reported once in the log, and so are events of a type the provider no longer takes.

**Several ids**
- One id is enough for most mods: one state type and any number of event types.
- Providers begin one after another, in the order they were registered. Each gets `OnReplayBegin` and then its start value before the next one begins.
- If one provider needs another's value when it begins, register the other first.

## Which runs

- Every attempt is its own run.
- Calibration runs, replays and offline renders are not recorded.
- Runs TUFReplay does not save are dropped with their values: runs without forward progress and runs without native input.

Next: [4. State and event types](4-state-types.md)
