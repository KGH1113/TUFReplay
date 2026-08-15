<div align="center">

![TUFReplay](https://capsule-render.vercel.app/api?type=waving&height=220&color=0:050505,45:101827,100:5eead4&text=TUFReplay&fontColor=e6fffb&fontAlignY=38&desc=Clear,%20Record,%20Edit,%20Upload,%20Submit,%20Wait%20->%20Clear,%20Submit%20&descAlignY=58&animation=fadeIn)

[![Runtime](https://img.shields.io/badge/runtime-ADOFAI%20%2F%20Unity-111827?style=for-the-badge&logo=unity&logoColor=white)](https://store.steampowered.com/app/977950/A_Dance_of_Fire_and_Ice/)
[![Mod Loader](https://img.shields.io/badge/mod%20loader-UnityModManager-7c3aed?style=for-the-badge)](https://www.nexusmods.com/site/mods/21)
[![Patching](https://img.shields.io/badge/patching-Harmony-f43f5e?style=for-the-badge)](https://github.com/pardeike/Harmony)
[![Build](https://img.shields.io/badge/build-.NET%20SDK-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Target](https://img.shields.io/badge/target-netstandard2.1-2563eb?style=for-the-badge)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Database](https://img.shields.io/badge/database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://sqlite.org/)
[![API](https://img.shields.io/badge/api-AdofaiIpc-f97316?style=for-the-badge)](#adofaiipc-api)

<br />

![Tech stack](https://skillicons.dev/icons?i=cs,dotnet,unity,sqlite,bash)

**ADOFAI will now automatically records your clear, and you can use it for submitting to TUF**

</div>

<div align="center">
  <img
    src="docs/media/showcase.gif"
    alt="showcase-gif"
    width="900"
  />
  <br />
</div>

## Overview

TUFReplay is a UnityModManager mod for **A Dance of Fire and Ice**. It records OS-native keyboard state changes for replay keyviewer/display output, records CReplay-style hit contexts for game playback, stores play records in a local SQLite database, exposes those records through AdofaiIpc, and plays saved runs directly from the companion web UI.

The project preserves low-level play data and, for new recordings, the resolved margin of each accepted hit. The resolved margin lets timeline scrubbing rebuild ADOFAI's canonical judgment tracker exactly, while older recordings remain playable by deriving margins from their existing hit contexts. When automatic recording is enabled, TUFReplay can also capture a run's microphone audio as 48 kHz mono PCM16 WAV data.

## Features

- Records OS-native keyboard state changes and hit contexts for every custom `.adofai` run, saving activity runs only after native input is captured.
- Suspends native keyboard capture and replay emission while the UnityModManager window is open.
- Stores ADOFAI's final X-Accuracy for each run so clients can display it without replaying judgment calculations.
- Stores each run's judgment difficulty and compact per-judgment counts for activity inspection.
- Stores lean activity records, replay payloads, immutable level revisions, and recorder timezone context in SQLite. Level files themselves are never copied into the database; visits point to a shared level row containing its source, local path, and gameplay hash.
- Snapshots song, chart creator, and artist metadata from each local `.adofai` file for activity history.
- Removes level and app sessions that close without any saved runs.
- Exposes local IPC methods for activity browsing and health checks through AdofaiIpc.
- Serves chart text to the companion web UI only while the local file still exists, decodes successfully, and matches the recorded gameplay hash.
- Wipes ADOFAI to black and verifies a chosen replay level with the game's own level decoder before opening it; mismatches restore the previous screen and keep the web chooser open, while verified levels continue directly into replay from the run's recorded start tile.
- Stores a versioned gameplay hash so replays can use a visually different `.adofai` file with the same tiles, timing settings, and judgment-affecting events. New records use SHA-256 hash v4, which treats the `.adofai` format version, run pitch, and hit-sound selection and volume as playback or serialization details rather than chart identity. On startup, verified v1-v3 rows are migrated and merged into v4; missing, changed, or unverifiable level files leave the original rows intact.
- Lets the web UI launch ADOFAI's native level picker without uploading local level contents to the browser.
- Keeps recording input after a clear until the editor returns so post-clear keyviewer input is preserved.
- Captures microphone audio from countdown through the clear screen until editor return, or until fail or abort, and temporarily saves it for each valid run. Microphone input is enabled by default, but the web menu can turn it off completely; while off, TUFReplay does not request permission, enumerate devices, launch the macOS helper, or arm capture.
- Streams microphone WAV files into a separate `tufreplay.microphones.sqlite` database without loading the full recording into memory; this isolates large BLOB writes from activity-run writes. Temporary recordings expire after three days unless the web UI keeps them permanently, and recordings can be deleted without deleting their runs.
- Lets the web activity menu delete an entire run, including its replay payload and microphone recording, while pruning closed activity sessions that no longer contain runs.
- Streams saved microphone audio alongside replay playback with pitch-aware timing, pause, retry, and terminal-state synchronization.
- Shows an in-game replay timeline HUD from countdown until replay termination, using the recorded terminal time for progress and ADOFAI's native pause path for pause and resume. Its linear timeline, transport controls, and separate elapsed/duration readouts live in a draggable floating panel whose position is retained for the current game session. The HUD loads from a platform AssetBundle and falls back safely if the bundle is unavailable.
- Optionally identifies TUFHelperLite-downloaded levels through TUFHelperLite's integration resolver for future TUF submission workflows.
- Provides the project foundation for replay playback and TUF clear submission.
- Supports English and Korean throughout the companion web UI, using the saved language choice first and the browser language on first visit.

## Runtime

TUFReplay runs inside ADOFAI through UnityModManager.

Required at runtime:

- A Dance of Fire and Ice
- UnityModManager
- AdofaiIpc 0.3.0 or newer; a missing installation is attempted automatically
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

TUFHelperLite is optional. When installed, TUFReplay resolves its downloaded level paths to public TUF forum IDs; recording itself does not depend on it.

### Optional mod integration

Other mods can detect a TUFReplay-owned replay operation without taking a compile-time dependency on TUFReplay. Resolve the public type `TUFReplay.ReplayRuntime` from the loaded TUFReplay assembly and read its static `IsPlaybackActive` property. The property is true throughout replay preparation, level loading, playback, and the return to the editor. `ReplayRuntime.ApiVersion` is `1` for this contract.

Reflection consumers should cache the resolved type and property getter, query the value only at relevant lifecycle boundaries, and treat a missing type, property, or assembly as an inactive replay.

## Repository Layout

- `TUFReplay/`: UnityModManager mod source.
- `TUFReplay.Unity/`: Unity 6.3 project for the replay timeline prefab, Canvas graphics, shader, and platform AssetBundle builder.
- `web/`: Bun/Vite companion web UI, managed as a workspace package.
- `TUFReplay.MicrophoneCapture.Mac/`: Xcode project for the AVFoundation helper used for macOS microphone permission and capture.
- `scripts/run.sh`: single entry point for build, package, helper, and shell validation workflows.
- `scripts/workflows/`, `scripts/tasks/`, `scripts/lib/`: workflow orchestration, independently runnable tasks, and shared shell utilities.

## Build

Copy the environment template and adjust paths if your setup differs from the macOS Steam default:

```bash
cp .env.example .env
```

Build and install the mod:

```bash
./scripts/run.sh build
```

The build script:

- Builds the fixed launcher, versioned update engine, and TUFReplay payload.
- Installs DLLs, native libraries, helpers, and assets under `Runtime/versions/<version>` while keeping settings and `Data/` at the mod root.
- Copies the bundled microphone calibration chart, `calibration_old.ogg`, and its precomputed waveform into `Assets/calibration`; packaging fails if any calibration asset is missing.
- On macOS, builds the helper's Xcode Release scheme, verifies its self-test and universal arm64/x86_64 executable, ad-hoc signs it, and installs the app with its own microphone usage description.
- Runs the C# WAV, schema migration, incremental BLOB, and cascade tests on macOS.
- Installs the mod into `Mods/TUFReplay` by default.

The fixed AdofaiIpc dependency shim selects a versioned bootstrap before TUFReplay starts. A missing AdofaiIpc installation is downloaded and verified once per process. Disabled, outdated, install-failure, and load-failure states stop the TUFReplay core and are shown in the shared AdofaiIpc dependency dialog without changing the user's UMM setting. The TUFReplay update engine verifies the complete ZIP and stages its bundled bootstrap as `Trial`; that bootstrap is used on the next game launch. If the matching TUFReplay runtime fails to initialize, its bootstrap trial is discarded while the current runtime remains active.

The first TUFReplay release using `AdofaiIpc.DependencyShim.dll` must be installed manually once. Later releases update the versioned bootstrap without overwriting a loaded DLL.

The Unity Mod Manager GUI includes a `Receive beta updates` toggle. It is disabled by default and saved to `UpdateSettings.json`; changes apply on the next game launch. The beta channel selects the highest compatible stable or prerelease SemVer from GitHub Releases. Disabling the channel never automatically downgrades an installed beta build.

Important environment variables:

- `ADOFAI_DIR`: ADOFAI install directory.
- `ADOFAI_MODS_DIR`: ADOFAI Mods directory.
- `ADOFAI_MANAGED`: Unity managed assembly directory.
- `DOTNET_EXE`: .NET SDK executable.
- `ADOFAI_IPC_DLL`: AdofaiIpc assembly path.
- `ADOFAI_IPC_BOOTSTRAP_DLL`: AdofaiIpc bootstrap assembly path.
- `ADOFAI_IPC_DEPENDENCY_SHIM_DLL`: fixed AdofaiIpc dependency shim assembly path.
- `ADOFAI_IPC_MIGRATION_DLL`: AdofaiIpc migration assembly path.
- `ADOFAI_IPC_INFO_JSON`: AdofaiIpc metadata path used by the package workflow for version verification.
- `TUFREPLAY_INSTALL_DIR`: install output override.

Create a clean shareable package:

```bash
./scripts/run.sh package
```

The package script creates an optimized Release build in `build/TUFReplay.zip` without copying data from an installed `Mods/TUFReplay` directory. It also creates `build/TUFReplay.update.json`, containing the version, package size, SHA-256, and packaged runtime path. Both files must be attached to a GitHub release for auto-update. The script verifies every packaged managed dependency, includes the Windows x64 SQLite native library from the `SourceGear.sqlite3` NuGet package, and excludes debug symbols and local database/log data.

Build only the macOS helper or validate the shell layer with:

```bash
./scripts/run.sh unity-ui
./scripts/run.sh mac-helper
./scripts/run.sh check
```

`unity-ui` rebuilds `ReplayTimelineRuntime.prefab` with Unity 6000.3.10f1 and writes `tufreplay_ui.bundle` files to `TUFReplay/Assets/mac`, `win`, and `linux`. The bundle contains the TUFHelperLite-style linear transport panel and MapleStory TMP font assets, without redistributing extracted ADOFAI images.

The entry point dispatches to workflows, workflows only sequence tasks, and tasks use the shared context, validation, dependency, and artifact libraries. Individual task scripts under `scripts/tasks` can also be run directly while diagnosing one build stage.

Beta releases use the same two assets and must be marked as a prerelease on GitHub. Beta.3 is the first full-runtime updater baseline. Beta.9 automatically installs the fixed dependency entrypoint for existing users, pauses TUFReplay for that session, and asks the user to reinstall only AdofaiIPC 0.3.0 before restarting the game. Later releases update both the runtime and versioned dependency bootstrap in place. Beta.7 accepts direct updates from Beta.5 and safely retries transient SQLite locks during the first activity-session write after migration. Beta.8 reduces long-session GC and web UI overhead, bounds activity polling, moves microphone finalization and replay audio file I/O off latency-sensitive paths, and migrates verified legacy gameplay hashes. Current builds use gameplay hash v4 so equivalent charts saved with different `.adofai` format versions remain compatible.

## Web Development

Install the Bun workspace dependencies from the repository root:

```bash
bun install
```

Run the companion web UI:

```bash
VITE_WEB_ADOFAI_EMBED_URL=http://127.0.0.1:5173/embed/chart bun run web:dev
```

The web UI bundles English and Korean translation resources under `web/src/i18n/locales`. The language menu stores the explicit selection in `localStorage`; without a saved selection, Korean browser locales use Korean and all other locales use English.

`VITE_WEB_ADOFAI_EMBED_URL` is required. When it is missing or invalid, the chart area shows a configuration warning instead of loading a hardcoded fallback URL.

To run the UI against the bundled activity, chart, run, and replay mock data instead of AdofaiIpc, open the development URL with `?mock=1` (for example, `http://localhost:5174/?mock=1`) or start Vite with `VITE_USE_MOCK_ACTIVITY=true`.

Test, type-check, or build the web workspace:

```bash
bun run web:test
bun run web:typecheck
bun run web:build
```

The web UI is built and deployed independently. The `build` and `package` workflows continue to build and package only the ADOFAI mod.

The browser reads TUF metadata through the same-origin `/api/tuf/*` path to avoid CORS failures. The Vite development and preview servers proxy that path to `https://api.tuforums.com`; production hosting must configure the equivalent rewrite while preserving the remaining path (for example, `/api/tuf/v2/database/levels/byId/871` → `https://api.tuforums.com/v2/database/levels/byId/871`).

## Web Deployment

GitHub Actions runs the web checks for pushes to `main` and `dev` and for pull requests targeting `main`. A successful push to `main` then connects to the home server through Tailscale SSH and restarts the Docker Compose app managed by the user-level `tuf-replay-web.service` unit.

The server checkout must exist at `/srv/TUFReplay`. The container exposes Vite preview on port 4173 and binds it to `127.0.0.1:4174` on the host by default. Set `TUF_REPLAY_WEB_PORT` in the server checkout's `.env` to override the host port. The production build embeds `https://web-adofai.impl1113.dev/embed/chart`; set `VITE_WEB_ADOFAI_EMBED_URL` in the same `.env` to override it.

The deploy workflow requires these GitHub Actions secrets:

- `TS_OAUTH_CLIENT_ID`
- `TS_OAUTH_SECRET`

The Tailscale OAuth client must be permitted to create an ephemeral `tag:gh-runner` node and use Tailscale SSH to reach `kgh`.

## Formatting

The repository uses separate deterministic formatters for each source tree:

- C# uses [CSharpier](https://csharpier.com/) 1.3.0 with a 120-character print width.
- Web TypeScript, JavaScript, JSON, and CSS use Biome 2.5.3 with a 100-character print width.

Restore the repository-local CSharpier tool after cloning:

```bash
dotnet tool restore
```

Format or check all C# sources:

```bash
dotnet csharpier format TUFReplay
dotnet csharpier check TUFReplay
```

Format or check the web workspace:

```bash
bun run web:format
bun run web:format:check
bun run web:biome
```

The checked-in VS Code settings select CSharpier for C# and Biome for web files, with format-on-save enabled for both. Install the `csharpier.csharpier-vscode` and `biomejs.biome` extensions to use those settings.

## AdofaiIpc API

The local API is intended for the companion web UI and development tools. TUFReplay requires
AdofaiIpc protocol version 2. Clients should probe `/ipc/health`, wait for the `tuf-replay`
namespace to reach `ready`, and then call TUFReplay through:

```http
POST /ipc
Content-Type: application/json
```

```json
{
  "namespace": "tuf-replay",
  "method": "health.get",
  "params": {},
  "id": "optional-client-id"
}
```

Registered methods:

- `health.get`
- `activity.app-sessions.list`
- `activity.level-session.get`
- `activity.level-session.runs.list`
- `activity.level-session.chart.get`
- `activity.logical-level.get`
- `activity.logical-level.runs.list` (`appSessionIds` scopes the logical level's runs to the selected day)
- `activity.logical-level.chart.get`
- `activity.run.delete` (`runId` identifies the run; active replays cannot be deleted)
- `replay.play`
- `replay.status.get`
- `replay.level-file.pick` (waits for selection and in-game gameplay-hash verification, then returns `selected`, `mismatch`, `cancelled`, or `error`)
- `microphone.devices.get`
- `microphone.enabled.set` (`enabled` is a boolean; access changes are locked during gameplay and calibration)
- `microphone.device.select` (`deviceId` is the opaque ID returned by `microphone.devices.get`, or `null` for the system default)
- `microphone.calibration.start`
- `microphone.calibration.status.get`
- `microphone.calibration.result.get`
- `microphone.calibration.preview.play`
- `microphone.calibration.preview.stop`
- `microphone.calibration.offset.set`
- `microphone.calibration.volume.set`
- `microphone.calibration.close`

TUFReplay registers its namespace as `initializing` while handlers are being attached and marks it
`ready` only after feature initialization completes. AdofaiIpc rejects premature calls with
`namespace_initializing`; an initialization failure is exposed as `namespace_error`.

`health.get` returns the TUFReplay namespace protocol and installed mod version:

```json
{
  "Ok": true,
  "Mod": "TUFReplay",
  "ModVersion": "0.1.0-beta.9",
  "ProtocolVersion": 4,
  "ServerVersion": 1
}
```

Web clients must compare `ProtocolVersion` with the protocol they support before calling other
TUFReplay methods. A missing or different protocol version means the installed mod is incompatible.
The companion web UI asks the user to fully quit and restart ADOFAI so the startup updater can install
a compatible TUFReplay release. `ServerVersion` remains as a legacy compatibility field and is not the
TUFReplay namespace protocol version.

Calibration is a transient session: its run and WAV are not written to the activity database. A successful clear exposes 2,048-bin song and microphone waveforms to the web editor. The precomputed song reference is aligned from ADOFAI's actual playback sample position on the recorded run timeline. Preview playback runs in ADOFAI while the browser polls the game clock; the saved global offset and `-20 dB` to `+20 dB` microphone gain (`0 dB` by default) are applied to calibration previews and all stored microphone replays.

## Tech Stack

- **C# / .NET SDK**: mod implementation and build tooling.
- **netstandard2.1**: target framework for Unity compatibility.
- **UnityModManager**: ADOFAI mod loading.
- **AdofaiIpc**: shared localhost IPC listener, namespace routing, and dependency bootstrap.
- **Harmony**: game method patching for recording hooks.
- **TUFHelperLite**: optional public TUF level ID resolution for downloaded charts.
- **SQLite / Microsoft.Data.Sqlite**: local play record storage.
- **Newtonsoft.Json**: metadata and API JSON serialization.
- **Bun / React / Vite**: companion activity explorer and embedded chart bridge.
- **Bash / .env**: local build and install configuration.

## Special Thanks

- **Teo** — Gave his idea to me and started this project.
