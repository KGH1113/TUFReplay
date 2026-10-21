# macOS CGEvent recording

The macOS recording backend uses a passive session event tap appended after
existing session filters. The IOHID recording backend and its HID usage mapper
have been removed. Input Monitoring permission is required.

## Capture contract

- Record key down/up, modifier changes, and mouse-button down/up.
- Exclude cursor movement, dragging, and scrolling.
- Preserve keyboard virtual key codes 0–127. Mouse buttons use 128–159
  (128 plus the Quartz button number), within the existing ushort CSV format.
- Merge input devices. Remapped input is observed at the session event stream.
- Interpret CGEvent timestamps as nanoseconds since startup, independently of
  the microphone's Mach-tick clock conversion.
- Suspend recording outside the game capture window. On return, discard older
  queued events and record differences in held-key/button state.

## Preventing input delivery stalls

The event tap has its own native thread and RunLoop. Its callback only reads
event fields, updates bounded state, and enqueues a fixed-size value into an
8,192-entry SPSC ring. It never calls C#, waits for Unity, takes a managed lock,
or posts input. A separate managed bridge drains batches. Native wakeup signals
are coalesced rather than accumulating one semaphore credit per event.

Modifier sides are decoded from the event's device-specific flag bits, without
querying WindowServer on the callback path. State queries happen at startup,
capture-window synchronization, and tap recovery. Shutdown removes and
invalidates the tap on its owning thread, then joins the bridge before freeing
its native context and pinned buffers. The source stays open across retries.

## Incomplete records

Queue overflow, tap timeout/disablement, capture-thread failure, read failure,
startup failure, or an event arriving more than one second late prevents a
playable replay artifact from being created. Runs with forward progress are
still saved as activity history, including when capture failure left no inputs.
The first detected failure category is stored in the existing replay unavailable
reason field. The companion UI explains that cause and a next action in English
and Korean. Partial input/hit CSV artifacts are not retained for playback.

The recorder can resynchronize/recover, but recovery does not make the affected
run complete. Idle faults are consumed before a new run begins so they do not
invalidate unrelated later recordings.

## Verification and installation

Verified with:

- `./scripts/run.sh mod-check`: universal macOS native library, offline native
  callback tests, one-million-event concurrent queue test, C# regression tests,
  Unity/Mono compatibility, and all 13 updater tests passed.
- `./scripts/run.sh web-check`: 133 tests, TypeScript, Biome, and build passed.
- `./scripts/run.sh check`: shell syntax checks passed; ShellCheck was not
  installed.
- `git diff --check`: passed.

The offline callback tests construct events without installing an event tap or
posting input to macOS. No game or long-session gameplay test was performed.
Actual event-tap permission behavior, Karabiner compatibility, and recurrence
of the reported focus-dependent input delay still require gameplay testing.

The build was not installed. From this worktree, run `./scripts/run.sh build`
to build, verify, and install before gameplay testing. Install the mod assembly
and `libTUFReplayInput.dylib` together: the capture ABI is now version 3.
