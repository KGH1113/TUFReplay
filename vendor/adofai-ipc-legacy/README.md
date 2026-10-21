# Legacy updater compatibility controls

These three unmodified DLLs and the bootstrap manifest come from the actually published
[TUFReplay v0.2.0-beta.4 ZIP](https://github.com/KGH1113/TUFReplay/releases/download/v0.2.0-beta.4/TUFReplay.zip),
downloaded for verification on 2026-10-21. ZIP SHA-256:
`5728993cec95ba0adadd04804ffb6dd7bcbbdaf9fe5f96c64564bbfd3b2f70b3`.

They remain in each versioned runtime for several releases because the old update engine
validates their presence and the old launcher stages the real bootstrap DLL. They are
not the SDK entrypoint and are never seeded by the new launcher. Keep this compatibility
payload until a deliberate future decision ends support for skipping the initial SDK release.

The published bootstrap manifest intentionally still names the old launcher. The SDK
payload commits a new root `Info.json` only after SDK initialization succeeds. The new
launcher uses the distinct assembly identity `TUFReplay.SdkLauncher` to avoid rebinding
to the already loaded legacy `TUFReplay.Bootstrap` during the transition run.

ADOFAI-IPC license: see `../adofai-ipc-runtime/LICENSE`.
