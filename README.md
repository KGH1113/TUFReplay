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

Replay engine v2 preserves OS-native input, the resolved margin of every accepted hit, and its recorded timeline timestamp. Replays from the previous Skyhook-based engine remain visible as activity history after upgrading but are not playable. When automatic recording is enabled, TUFReplay can also capture a run's microphone audio as 48 kHz mono PCM16 WAV data.

New recordings keep hit timestamps nondecreasing even when the game song clock moves backward. Buffered native inputs retain their independently mapped timestamps and input ordering; recording a hit does not push an earlier buffered input forward. Clear and terminal times include both recorded inputs and hits. The song-relative time base and game input offset remain unchanged.

## Features

- Records OS-native input state changes and hit contexts for custom `.adofai` runs. Runs with forward progress and an input capture failure are retained as activity history with a specific reason and cannot be replayed, even when no input was collected.
- On macOS, a passive CGEvent session tap records keyboard and mouse-button transitions after OS remapping. It excludes mouse motion, dragging, and scrolling. A dedicated native RunLoop queues fixed-size events without calling managed code or waiting for the game thread; a separate bridge drains them. Devices are merged, mouse buttons use native codes 128–159, and existing keyboard codes and replay CSV format remain compatible. Input Monitoring permission is required. See [macOS CGEvent recording](docs/macos-cgevent-recording.md) for the capture contract, verification, and installation.
- Focus loss suspends input recording; focus return discards earlier input backlog and synchronizes held keys and buttons. Queue overflow, tap timeout/disablement, capture-thread failure, and events arriving over one second late make the run unavailable for replay. Activity history explains the failure in English or Korean. The recorder attempts recovery for later runs without treating a partial run as complete.
- Suspends native input recording and replay emission while the UnityModManager window is open.
- Schedules replay input on a dedicated worker without making Unity wait for native input emission. Pause, focus loss, seek, and stop commands invalidate pending input; accepted key presses are released by the worker before it restores a new playback state. Shutdown completes asynchronously so Windows keyboard hooks can continue receiving game-thread messages.
- Stores ADOFAI's final X-Accuracy for each run so clients can display it without replaying judgment calculations.
- Stores each run's judgment difficulty and judgment system. Legacy and non-competitive runs keep the classic Perfect bucket, while modern competitive runs preserve Perfect−, X-Perfect, and Perfect+ separately.
- Stores lean activity records, replay payloads, immutable level revisions, and recorder timezone context in SQLite. Level files themselves are never copied into the database; visits point to a shared level row containing its source, local path, and gameplay hash.
- Finalizes game-derived recording state on the game thread, then serializes replay payloads and writes activity sessions/runs on an ordered background queue. Retries retain their own completed input buffers, and microphone retention waits for the activity transaction to succeed. Normal quit and mod unload allow up to five seconds to drain pending activity writes.
- Prepares held-key checkpoints before replay startup so seeking a long recording restores input state with a binary search and at most 2,047 transitions, preserving chord order and native key metadata.
- Keeps the native recording input source ready between runs; retries change the capture boundary instead of joining and reopening the platform hook. Disabling the mod releases the source asynchronously.
- Snapshots song, chart creator, and artist metadata from each local `.adofai` file for activity history.
- Removes level and app sessions that close without any saved runs.
- Exposes local IPC methods for activity browsing and health checks through AdofaiIpc.
- Serves chart text to the companion web UI only while the local file still exists, decodes successfully, and matches the recorded gameplay hash.
- Wipes ADOFAI to black and verifies a chosen replay level with the game's own level decoder before opening it; mismatches restore the previous screen and keep the web chooser open, while verified levels open with path editing locked and continue directly into replay from the run's recorded start tile.
- Stores SHA-256 gameplay hash v4 so replays can use a visually different `.adofai` file with the same tiles, timing settings, and judgment-affecting events. Imported activity resolves its current hash lazily when the original level file is still available.
- Lets the web UI launch ADOFAI's native level picker without uploading local level contents to the browser.
- Keeps recording input after a clear until the editor returns so post-clear keyviewer input is preserved.
- Captures microphone audio from countdown through the clear screen until editor return, or until fail or abort, and temporarily saves it for each valid run. Microphone input is enabled by default, but the web menu can turn it off completely; while off, TUFReplay does not request permission, enumerate devices, launch the macOS helper, or arm capture.
- Streams microphone WAV files into a separate `tufreplay.microphones.sqlite` database without loading the full recording into memory; this isolates large BLOB writes from activity-run writes. Temporary recordings expire after three days unless the web UI keeps them permanently, and recordings can be deleted without deleting their runs.
- Lets the web activity menu delete an entire run, including its replay payload and microphone recording, while pruning closed activity sessions that no longer contain runs.
- Streams saved microphone audio alongside replay playback with pitch-aware timing, pause, retry, and terminal-state synchronization. New recordings align the first microphone sample to the native-input clock after the game timeline resumes, so EnhancedCountdown waits do not become replay offsets. On macOS the helper supplies the first written sample's host timestamp; other platforms anchor Unity's microphone cursor to the same monotonic clock.
- Replays restore the recorded game input offset when available. Older records without that value keep the current game setting; input/hit timestamp differences are not used to guess calibration.
- Shows an in-game replay timeline HUD from countdown until replay termination, using the recorded terminal time for progress and ADOFAI's native pause path for pause and resume. Its linear timeline, transport controls, and separate elapsed/duration readouts live in a draggable floating panel whose position is retained for the current game session. The HUD loads from a platform AssetBundle and falls back safely if the bundle is unavailable.
- Aggregates overlapping timeline judgments into display columns, preserving the most severe visible judgment and keeping dense replays below Unity's UI vertex limit. The progress fill rebuilds only after a visible half-pixel change while the playhead continues to track replay time.
- Guides replay handoff from the web UI into ADOFAI with an acknowledged, persistent focus prompt; blocks duplicate replay launches, protects the editor play command during preparation, and waits briefly after focus stabilizes before playback starts.
- Optionally identifies TUFHelperLite-downloaded levels through TUFHelperLite's integration resolver for future TUF submission workflows.
- Provides the project foundation for replay playback and TUF clear submission.
- Supports English and Korean throughout the companion web UI, using the saved language choice first and the browser language on first visit.
- Groups revisions of the same `.adofai` path into one web activity card, keeping different files in one TUFHelperLite download separate. Cards for a day are ordered by the last time each level was opened, newest first. The card shows the file path relative to the downloaded level folder, or the file name when that folder cannot be identified. Only runs compatible with the most recently played gameplay revision can be opened; incompatible runs remain stored, keep their historical counts, and are explained by warning tooltips.
- Counts a run as a clear only when it starts at tile zero, reaches the clear terminal state, and does not use No-Fail mode.

## Runtime

TUFReplay runs inside ADOFAI through UnityModManager.

Required at runtime:

- A Dance of Fire and Ice
- UnityModManager
- AdofaiIpc 1.0.0 or newer; a missing installation is attempted automatically
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

TUFHelperLite is optional. When installed, TUFReplay resolves its downloaded level paths to public TUF forum IDs; recording itself does not depend on it.

### Optional mod integration

Other mods can detect a TUFReplay-owned replay operation without taking a compile-time dependency on TUFReplay. Resolve the public type `TUFReplay.ReplayRuntime` from the loaded TUFReplay assembly and read its static `IsPlaybackActive` property. The property is true throughout replay preparation, level loading, playback, and the return to the editor. `ReplayRuntime.ApiVersion` is `1` for this contract.

Reflection consumers should cache the resolved type and property getter, query the value only at relevant lifecycle boundaries, and treat a missing type, property, or assembly as an inactive replay.

## Repository Layout

- `TUFReplay/`: UnityModManager mod source, organized first by feature and then by concrete role.
  - `Activity/`: `Models`, `Queries`, `Tracking`, `Repositories`, `Migrations`, `Charts`, and `Ipc`.
  - `Replay/`: `Models`, `Sessions`, `Preparation`, `Transport`, `Playback`, `NativeInput`, `Levels`, `Timeline`, `Patches`, and `Ipc`.
  - `Recording/`: `Models`, `Sessions`, `Input`, `Activity`, `Microphone`, and `Patches`.
  - `Microphone/`: `Models`, `Devices`, `Capture`, `Playback`, `Processing`, `Timing`, `Recording`, `Repositories`, and `Ipc`.
  - `Calibration/`: `Models`, `Sessions`, `Analysis`, `Playback`, `Levels`, and `Ipc`.
  - `Composition/`: the mod composition root and feature registry. This is separate from the fixed launcher assembly in `TUFReplay.Bootstrap/`.
  - `Shared/`: database, IPC, settings, native-input, and Unity primitives shared by multiple features.
- `TUFReplay.Bootstrap/`: fixed launcher that selects and loads a versioned TUFReplay runtime.
- `TUFReplay.UpdateEngine/`: versioned update and package installation engine.
- `TUFReplay.Tests/`: executable C# test harness, grouped into activity/database, microphone/calibration, and replay/native-input suites.
- `TUFReplay.UpdateTests/`: updater test suite linked into the main C# test harness.
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
- Runs the C# WAV, schema-generation/reset, incremental BLOB, and replay-contract tests on macOS.
- Installs the mod into `Mods/TUFReplay` by default.

The fixed AdofaiIpc dependency shim selects a versioned bootstrap before TUFReplay starts. A missing AdofaiIpc installation is downloaded and verified once per process. Disabled, outdated, install-failure, and load-failure states stop the TUFReplay core and are shown in the shared AdofaiIpc dependency dialog without changing the user's UMM setting. The TUFReplay update engine verifies the complete ZIP and stages its bundled bootstrap as `Trial`; that bootstrap is used on the next game launch. If the matching TUFReplay runtime fails to initialize, its bootstrap trial is discarded while the current runtime remains active.

The first TUFReplay release using `AdofaiIpc.DependencyShim.dll` must be installed manually once. Later releases update the versioned bootstrap without overwriting a loaded DLL.

Standard builds include a `Receive beta updates` toggle in the Unity Mod Manager GUI. Auto-submission builds show their separate update channel and version. It is disabled by default and saved to `UpdateSettings.json`; changes apply on the next game launch. The beta channel selects the highest compatible stable or prerelease SemVer from GitHub Releases. Disabling the channel never automatically downgrades an installed beta build.

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
- `TUFREPLAY_BUILD_FLAVOR`: `standard` (default) or `auto-submission`.
- `TUFREPLAY_BUILD_VERSION`: explicit package version; required for auto-submission builds.

Create a clean shareable package:

```bash
./scripts/run.sh package
```

The package script creates an optimized Release build in `build/TUFReplay.zip` without copying data from an installed `Mods/TUFReplay` directory. It also creates `build/TUFReplay.update.json`, containing the version, package size, SHA-256, and packaged runtime path. For standard builds, both files are attached to a GitHub release. Auto-submission builds publish their package and manifest to the isolated home-server update channel. The script verifies every packaged managed dependency, includes the Windows x64 SQLite native library from the `SourceGear.sqlite3` NuGet package, and excludes debug symbols and local database/log data.

Build only the macOS helper or validate the shell layer with:

```bash
./scripts/run.sh unity-ui
./scripts/run.sh mac-helper
./scripts/run.sh check
./scripts/run.sh mod-check
```

`mod-check` builds and runs the C# checks without installing the result into the game.

`unity-ui` rebuilds the replay timeline and generic runtime notification prefabs with Unity 6000.3.10f1 and writes `tufreplay_ui.bundle` files to `TUFReplay/Assets/mac`, `win`, and `linux`. The bundle contains the TUFHelperLite-style linear transport panel, uGUI toast/persistent-error UI, and MapleStory TMP font assets, without redistributing extracted ADOFAI images.

The entry point dispatches to workflows, workflows only sequence tasks, and tasks use the shared context, validation, dependency, and artifact libraries. Individual task scripts under `scripts/tasks` can also be run directly while diagnosing one build stage.

Beta releases use the same two assets and must be marked as a prerelease on GitHub. Version 0.2.0-beta.1 introduces replay engine `tufreplay.replay.v2`, payload format 1, and a one-time web notice for records from the previous engine. The new activity database uses application ID `0x54554652` and schema version 1, while gameplay identity uses hash v4. Schema v15 activity and microphone records are imported while the source is preserved as `tufreplay.pre-0.2.sqlite`; replay payloads recorded by the previous engine are intentionally not imported. Schema v14 and earlier databases log a warning and are replaced with a fresh current database. App SemVer, activity schema, replay engine, replay payload, and gameplay hash versions are independent compatibility boundaries.

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

New runs refresh the current day's list in the background while retaining the selected tile/run, the open run sidebar, and the chart's current view. When viewing cards below the top of the list, new cards preserve the visible card's scroll position. Refresh failures keep the existing chart and list available with a retry action; changing the day or gameplay revision resets the selection for that scope.

`VITE_WEB_ADOFAI_EMBED_URL` is required. When it is missing or invalid, the chart area shows a configuration warning instead of loading a hardcoded fallback URL.

To run the UI against the bundled activity, chart, run, and replay mock data instead of AdofaiIpc, open the development URL with `?mock=1` (for example, `http://localhost:5174/?mock=1`) or start Vite with `VITE_USE_MOCK_ACTIVITY=true`.

The web source uses a one-way layered architecture. Each layer is subdivided by domain where applicable:

```text
shared clients/UI → schemas → models → api → state/mocks → hooks
                  → components → sections → pages → app
```

High-level application services depend on application-owned message ports in `web/src/ports`; only the transport adapter and app composition root import the vendored AdofaiIpc SDK. WebSocket messages and HTTP metadata enter as unknown data and are validated by Zod in the API adapter layer. Feature messages update TanStack Query caches; calibration editing state stays in its feature reducer. Hooks act as page/component ViewModels: they own application state, derived display values, and commands, while pages and components focus on composition and rendering. The app composition root is the only place that selects the production or mock `AppApi` bundle. Canonical shadcn primitives live in `web/src/shared/ui` and cannot import domain code.

Tests live separately under `web/tests`, mirror the source domains, and use purpose-specific suffixes such as `*.unit.test.ts`, `*.contract.test.ts`, and `*.integration.test.tsx`. The architecture contract test enforces the allowed import direction.

Test, type-check, or build the web workspace:

```bash
bun run web:test
bun run web:typecheck
bun run web:biome
bun run web:build
```

The web UI is built and deployed independently. The `build` and `package` workflows continue to build and package only the ADOFAI mod.

The browser reads TUF metadata through the same-origin `/api/tuf/*` path to avoid CORS failures. The Vite development and preview servers proxy that path to `https://api.tuforums.com`; production hosting must configure the equivalent rewrite while preserving the remaining path (for example, `/api/tuf/v2/database/levels/byId/871` → `https://api.tuforums.com/v2/database/levels/byId/871`).

## Web and API Deployment

GitHub Actions deploys each branch to a separate Compose project on the home server:

| Branch | Website | Build flavor | Host loopback port |
| --- | --- | --- | --- |
| `main` | https://tufreplay.impl1113.dev | `standard` | 4174 |
| `dev` | https://tufreplay-dev.impl1113.dev | `standard` | 4175 |
| `feat/auto-submission` | https://tufreplay-auto.impl1113.dev | `auto-submission` | 4176 |

Main and dev serve the companion web UI. The auto-submission environment also runs the Rust API, worker, scheduler, PostgreSQL, Redis, and artifact storage. Separate deployment Compose files live under `deploy/`; the root `docker-compose.yml` remains available for local development. Each website exposes `/deployment.json` with its environment, build flavor, and deployed commit.

Web and API deployments run through GitHub Actions. Mod packages are built locally and uploaded by the operator. The workflows use Tailscale to reach the home server and require `TS_OAUTH_CLIENT_ID` and `TS_OAUTH_SECRET`. See [deployment setup and recovery](deploy/README.md) for the protected environment file, one-time routing bootstrap, and persistent data paths.

Standard mod releases use GitHub Releases. Auto-submission builds use the separate home-server feed at `https://tufreplay-auto.impl1113.dev/updates/auto-submission/latest.json`; they are not published as GitHub Releases. Installing a different flavor requires installing that flavor's package. The auto-submission website requires an auto-submission mod with matching submission protocol support before enabling submission features.

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

## AdofaiIpc messages

TUFReplay requires **AdofaiIpc 1.0.0**, WebSocket wire protocol **3**, and TUFReplay
namespace protocol **9**. The local connection uses `/ipc/ws` and the
`adofai-ipc.v3` subprotocol. The companion uses the canonical TypeScript SDK
source committed under `vendor/adofai-ipc`, with its license and source revision;
no published npm SDK package, linked sibling checkout, or registry release is required.

The `tuf-replay` namespace becomes ready after feature initialization. Both peers
send named commands and domain events over one connection. A transport acceptance
means a command was queued; the named result event indicates what happened.
Every result carries the original command id as `correlationId`. Domain failures
are `command.rejected` events with actionable `code` and `message` fields.
Commands are never replayed automatically after reconnecting.

| Command | Domain result |
| --- | --- |
| `health.read` | `health.snapshot` |
| `activity.sessions.read` | `activity.sessions.snapshot` |
| `activity.legacy-status.read` | `activity.legacy-status.snapshot` |
| `activity.level.read` | `activity.level.snapshot` |
| `activity.runs.read` | `activity.runs.snapshot` |
| `activity.chart.read` | `activity.chart.snapshot` |
| `activity.run.remove` | `activity.run.removed` |
| `replay.start`, `replay.state.read` | `replay.state.changed` |
| `replay.level-file.choose` | `replay.level-file.finished` |
| `microphone.devices.refresh`, `microphone.access.change`, `microphone.device.choose` | `microphone.devices.changed` |
| `microphone.offset.change`, `microphone.volume.change` | `microphone.timing.changed` |
| `microphone.recording.remove`, `microphone.recording.retain` | `microphone.recording.removed`, `microphone.recording.retained` |
| `microphone.recording.download` | `download.ready` |
| `calibration.start`, `calibration.state.read`, `calibration.preview.start`, `calibration.preview.stop`, `calibration.offset.change`, `calibration.volume.change`, `calibration.close` | `calibration.state.changed` |
| `calibration.result.read` | `calibration.result.ready` |

The mod also sends `activity.changed` after database persistence commits, replay
state changes at their lifecycle boundaries, microphone access changes when
capture becomes locked or unlocked, and calibration state and preview clock
updates. Preview clock updates are limited to 20 Hz; the UI interpolates its
playhead between samples. These notifications replace activity, replay, picker,
and calibration status polling. Activity history still loads bounded pages on
demand. A fresh namespace subscription receives health, replay and calibration
snapshots, and reconnecting refreshes the relevant application caches.

The native file picker emits its final selected/mismatch/cancelled/error result
when selection and gameplay verification finish. There is no picker-status loop
or second HTTP client. Peer disconnect cancels its pending selection operation.

`download.ready` contains `{url, byteLength}` for a one-use local streaming URL.
WAV bytes stream from SQLite through the dedicated download route; the browser
opens the URL normally and does not buffer or base64 encode the complete file.
The download route is a data path; command handling uses WebSocket messages.

Chart snapshots contain `{url, byteLength, metadata: {LevelSessionId, FloorCount}}`.
The chart's validated UTF-8 text streams through the same single-use download route;
it is never embedded in a WebSocket control message. This allows charts larger than
the IPC's 2MiB control-message limit without disconnecting or reloading activity history.
The web application depends on a text-download port, injects its browser adapter at
composition, verifies the received byte length, and cancels obsolete chart downloads.

The microphone timing dialog retains global offset and gain editing, transient
calibration waveforms, and game preview playback. Calibration recordings are not
written into activity history. Every calibration starts from raw uncorrected
capture timing. The saved offset and `-20 dB` to `+30 dB` gain remain applied to
stored microphone playback, with the existing true-peak limiter.

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
- **Potato** - Developed CReplay, a mod that was heavily referenced in this project.
