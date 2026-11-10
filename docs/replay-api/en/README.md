# Replay API

[한국어](../ko/README.md)

`TUFReplay.ReplayApi` lets overlay, key-viewer and other mods take part in TUFReplay's recordings and replays.

- **Follow replays.** While a run is replayed, your mod gets the replay's clock and recorded keys. The calls are the same in the game and in a render to video, so you write one replay path.
- **Save values with each run.** Your mod saves its own values with each recorded run, such as the attempt number, its settings or a heart rate, and gets each one back at the moment it had in the run.
- **Appear in videos.** A render puts your canvases in the video, or lays your own video over it.

`ApiVersion` is 2.

## Read in order

1. [Connect to TUFReplay](1-connect.md): find the API without a reference to TUFReplay.dll, and register.
2. [Follow a replay](2-follow-a-replay.md): show the replay's keys and clock instead of live input.
3. [Save values with each run](3-run-values.md): one id per value, the value a run starts with, values that change during it, and getting them back.
4. [State types](4-state-types.md): what a saved class can hold, and how to change it later.
5. [Render to video](5-render.md): canvases, frame capture and overlay videos.

[Reference](reference.md) lists every member, the log messages, the storage and what changed in ApiVersion 2. [Other APIs](other-apis.md) covers `ReplayRuntime` and `NativeInputPause`.
