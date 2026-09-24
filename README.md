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

TUF auto-submitted passes embed web-adofai using a small record-ID contract. The player owns replay downloads, integrity checks, streaming archive extraction, temporary OPFS storage, loading/retry UI and playback controls; the TUF frontend only opens/closes the embed. Standalone player links also support beta testing. See the [replay delivery contract](docs/replay-delivery-contract.md). The [local game E2E runner](docs/local-full-e2e-commands.md) prepares a real login account and OAuth client, then starts the backend, submission workers, companion UI, TUF frontend, and web-adofai together from their local source checkouts. It uses actual game IPC and recorded clears; gameplay still needs a player. The gameplay simulator remains unavailable, so this local setup uses the explicit trusted-tester policy.

TUFReplay is a UnityModManager mod for **A Dance of Fire and Ice**. It records OS-native keyboard state changes for replay keyviewer/display output, records CReplay-style hit contexts for game playback, stores play records in a local SQLite database, exposes those records through AdofaiIpc, and plays saved runs directly from the companion web UI.

Replay engine v2 preserves OS-native input, the resolved margin of every accepted hit, and its recorded timeline timestamp. Replays from the previous Skyhook-based engine remain visible as activity history after upgrading but are not playable. When automatic recording is enabled, TUFReplay can also capture a run's microphone audio as 48 kHz mono PCM16 WAV data.

New recordings keep hit timestamps nondecreasing even when the game song clock moves backward. Buffered native inputs retain their independently mapped timestamps and input ordering; recording a hit does not push an earlier buffered input forward. Clear and terminal times include both recorded inputs and hits. The song-relative time base and game input offset remain unchanged.

## Features

Trusted tester membership is managed in PostgreSQL through a separate [local administrator app](tools/trusted-testers-admin/README.md). Rust checks active membership at account/upload/submission authorization; TUF retains OAuth, account and final registration checks. See the [rollout guide](deploy/trusted-testers-admin.md) before switching away from the legacy TUF environment allowlist.

- Records OS-native keyboard state changes and hit contexts for every custom `.adofai` run, saving activity runs only after native input is captured.
- Suspends native keyboard capture and replay emission while the UnityModManager window is open.
- Stores ADOFAI's final X-Accuracy for each run so clients can display it without replaying judgment calculations.
- Stores each run's judgment difficulty and judgment system. Legacy and non-competitive runs keep the classic Perfect bucket, while modern competitive runs preserve Perfect−, X-Perfect, and Perfect+ separately.
- Stores lean activity records, replay payloads, immutable level revisions, and recorder timezone context in SQLite. Level files themselves are never copied into the database; visits point to a shared level row containing its source, local path, and gameplay hash.
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
- Guides replay handoff from the web UI into ADOFAI with an acknowledged, persistent focus prompt; blocks duplicate replay launches, protects the editor play command during preparation, and waits briefly after focus stabilizes before playback starts.
- Identifies TUFHelperLite-downloaded levels and streams P/G auto-submission evidence through one authenticated, reusable WebSocket per opened level. Each attempt has its own `run_start` → evidence → `run_fail`/`run_complete` lifecycle. Bounded capture starts before server approval, so immediate retries never wait for REST issuance or network I/O; idle connections create no runs. See the [level session protocol](docs/auto-submission-implementation/11-reusable-level-session.md).
- Registers saved Jipper Resourcepack, Jipper KeyViewer, DMNote, Impl DMNote, and ImplResourcePack visual presets through the companion web UI and authenticated mod IPC. Missing fonts/images can be attached during registration and are preserved in the preset bundle. DMNote imports the saved selected tab from multi-tab exports. Registration also accepts a separate CSS file, a fixed position and 10–400% keyviewer size on a 16:9 screen, and its global key-counter visibility setting (off by default). Submissions independently select at most one keyviewer and one overlay, with selections fixed for retries. See [the visual preset contract](docs/visual-presets-contract-2026-09-16.md) and [local E2E verification](docs/visual-presets-local-e2e-2026-09-16.md) for tested flows and remaining compatibility limits.
- Jipper Resourcepack, Jipper KeyViewer, and ImplResourcePack are detected automatically from the game’s installed mods and saved settings; registration does not ask for their configuration files. Version-suffixed or renamed UMM folders are identified by mod metadata. DMNote and Impl DMNote use exported JSON files. Jipper KeyViewer is a separate keyviewer source from Jipper Resourcepack; ImplResourcePack is overlay-only. Jipper Resourcepack uses `jipper-resourcepack`; standalone Jipper KeyViewer uses `jipper-keyviewer`. See [additional source formats](docs/visual-sources-2026-09-17.md) for the import contract and rendering boundary. See [automatic import verification](docs/visual-import-verification-2026-09-17.md).
- Visual snapshots include the original key/progress sprites, supported DMNote built-in fonts, pose images and attached CSS resources. The player uses source slice/tile geometry, TMP font metrics and portable CJK fallback chains. ImplResourcePack includes the game's original CJK font automatically; its default registration needs no attachments. See [source fidelity and automated verification](docs/visual-source-handoff-2026-09-21.md).
- Preset registration runs as a background mod job. The companion shows saved-settings, image/font processing, validation and server-registration stages, including the current file and completed file count. Inspection and registration reuse one snapshot, so large CJK fonts are not rebuilt between them. Missing attachments pause registration before any server upload. No estimated percentage is shown.
- Provides replay playback and the auto-submission capture, upload, review UI, and registration pipeline. The gameplay simulator is not implemented. The default mode remains `validator_unavailable`; the explicit `trusted_tester` rollout converts recorded clear results for TUF accounts on the backend allowlist and records that validation was skipped. Replay beta updates do not grant submission permission. See [implementation status and setup](docs/auto-submission-status.md).
- Supports English and Korean throughout the companion web UI, using the saved language choice first and the browser language on first visit.
- Shows a one-time browser notice when saved runs use the previous replay engine and cannot be played by the current engine.
- Groups revisions of the same TUF level or local level path into one web activity card. Only runs compatible with the most recently played gameplay revision can be opened; incompatible runs remain stored, keep their historical counts, and are explained by warning tooltips.
- Counts a run as a clear only when it starts at tile zero, reaches the clear terminal state, and does not use No-Fail mode.

## Runtime

TUFReplay runs inside ADOFAI through UnityModManager.

Required at runtime:

- A Dance of Fire and Ice
- UnityModManager
- AdofaiIpc 0.4.1 or newer; a missing installation is attempted automatically
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
  - `Visual/`: visual-preset `Domain`, wire `Contracts`, use-case `Application`, source-independent `Importing`, external `Infrastructure`, per-mod `Sources`, IPC handlers, and the composition facade.
  - `Composition/`: the mod composition root and feature registry. This is separate from the fixed launcher assembly in `TUFReplay.Bootstrap/`.
  - `Shared/`: database, IPC, settings, native-input, and Unity primitives shared by multiple features.
- `TUFReplay.Bootstrap/`: fixed launcher that selects and loads a versioned TUFReplay runtime.
- `TUFReplay.UpdateEngine/`: versioned update and package installation engine.
- `TUFReplay.Tests/`: executable C# test harness, grouped into activity/database, microphone/calibration, and replay/native-input suites.
- `TUFReplay.UpdateTests/`: updater test suite linked into the main C# test harness.
- `TUFReplay.Unity/`: Unity 6.3 project for the replay timeline prefab, Canvas graphics, shader, and platform AssetBundle builder.
- `web/`: Bun/Vite companion web UI, managed as a workspace package.
- `tools/auto-submission-e2e/`: standalone local web lab that uploads recorded clears to the real Rust server. TUF is mocked by default; `E2E_TUF_TARGET=local` registers real passes on the dedicated local TUF backend. See [E2E setup and usage](tools/auto-submission-e2e/README.md).
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

- `TUFREPLAY_ENV_FILE`: optional build environment file instead of the local `.env`; use `/dev/null` with explicit environment values for a reproducible release build.
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
```

Run verification without installing the mod:

```bash
./scripts/run.sh mod-check
./scripts/run.sh web-check
./scripts/run.sh visual-check # Cross-repository visual source contract (requires sibling consumers)
./scripts/run.sh visual-fixtures # Real source assets, after mod-check; JKV_SOURCE_ROOT may override the audit checkout
# Set TUF_VISUAL_TEST_DATABASE_URL to an existing disposable database; this test recreates its tables.
./scripts/run.sh visual-pipeline-check # Importer -> DB/API -> replay renderer and PNG pixel comparisons
./scripts/run.sh server-check
DATABASE_URL=postgres://USER@localhost:PORT/ISOLATED_TEST_DB ./scripts/run.sh server-check --integration
```

The server integration configuration recreates its database. Always give it an isolated test database, never a development or production database containing records to keep.

`unity-ui` rebuilds the replay timeline and generic runtime notification prefabs with Unity 6000.3.10f1 and writes `tufreplay_ui.bundle` files to `TUFReplay/Assets/mac`, `win`, and `linux`. The bundle contains the TUFHelperLite-style linear transport panel, uGUI toast/persistent-error UI, and MapleStory TMP font assets, without redistributing extracted ADOFAI images.

The entry point dispatches to workflows, workflows only sequence tasks, and tasks use the shared context, validation, dependency, and artifact libraries. Individual task scripts under `scripts/tasks` can also be run directly while diagnosing one build stage.

Beta releases use the same two assets and must be marked as a prerelease on GitHub. Version 0.2.0-beta.1 introduces replay engine `tufreplay.replay.v2`, payload format 1, the automatic submission pipeline, and a one-time web notice for records from the previous engine. The new activity database uses application ID `0x54554652` and schema version 1, while gameplay identity uses hash v4. Schema v15 activity and microphone records are imported while the source is preserved as `tufreplay.pre-0.2.sqlite`; replay payloads recorded by the previous engine are intentionally not imported. Schema v14 and earlier databases log a warning and are replaced with a fresh current database. App SemVer, activity schema, replay engine, replay payload, and gameplay hash versions are independent compatibility boundaries.

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

The web source uses a one-way layered architecture. Each layer is subdivided by domain where applicable:

```text
shared clients/UI → schemas → models → api → state/mocks → hooks
                  → components → sections → pages → app
```

AdofaiIpc and HTTP payloads enter the application as unknown data and are validated by Zod in the API layer. TanStack Query owns server and IPC state; calibration editing state stays in its feature reducer. Hooks act as page/component ViewModels: they own application state, derived display values, and commands, while pages and components focus on composition and rendering. The app composition root is the only place that selects the production or mock `AppApi` bundle. Canonical shadcn primitives live in `web/src/shared/ui` and cannot import domain code.

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

Auto-submission supports private R2 artifact storage with verified migration and local fallback. Visual preset images/fonts are uploaded separately as deduplicated SHA-256 objects; PostgreSQL retains metadata and references. Legacy inline presets and players remain supported. See [R2 deployment, migration and asset access](deploy/r2-storage.md).

With CDN delivery enabled, the manifest API issues 15-minute signed object URLs for replay evidence, keyviewer/overlay JSON, images and fonts. The player downloads these bodies directly from the Cloudflare Worker/R2 path; the home server only authorizes and returns metadata. The Worker verifies every grant before serving its shared immutable object cache. Packaged default visual assets can be registered once through the operator task, eliminating per-account uploads without making private objects anonymously accessible.

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
- `microphone.recording.export` (`runId` identifies a recorded run; returns a short-lived download URL)
- `replay.play`
- `replay.status.get`
- `replay.level-file.pick` (waits for selection and in-game gameplay-hash verification, then returns `selected`, `mismatch`, `cancelled`, or `error`)
- `microphone.devices.get`
- `microphone.enabled.set` (`enabled` is a boolean; access changes are locked during gameplay and calibration)
- `microphone.device.select` (`deviceId` is the opaque ID returned by `microphone.devices.get`, or `null` for the system default)
- `microphone.offset.set` (`offsetMs` updates the global replay microphone timing outside gameplay and calibration)
- `microphone.volume.set` (`volumeDb` updates the global replay microphone gain outside gameplay and calibration)
- `microphone.calibration.start`
- `microphone.calibration.status.get`
- `microphone.calibration.result.get`
- `microphone.calibration.preview.play`
- `microphone.calibration.preview.stop`
- `microphone.calibration.offset.set`
- `microphone.calibration.volume.set`
- `microphone.calibration.close`

The run card's microphone menu uses `microphone.recording.export` to request a one-use URL.
The browser opens that URL as a normal download; AdofaiIpc sends the WAV directly from
SQLite through its existing HTTP listener. The web UI never buffers or base64-encodes the
recording. Expired or already-used URLs require another export request.
The web workspace uses the matching `@adofai-ipc/client` 0.4.1 npm package.

TUFReplay registers its namespace as `initializing` while handlers are being attached and marks it
`ready` only after feature initialization completes. AdofaiIpc rejects premature calls with
`namespace_initializing`; an initialization failure is exposed as `namespace_error`.

`health.get` returns the TUFReplay namespace protocol and installed mod version:

```json
{
  "Ok": true,
  "Mod": "TUFReplay",
  "ModVersion": "0.2.0-beta.4",
  "BuildFlavor": "standard",
  "AutoSubmissionProtocolVersion": 0,
  "ProtocolVersion": 7,
  "ReplayEngineId": "tufreplay.replay.v2",
  "ReplayFormatVersion": 1,
  "ServerVersion": 1
}
```

Web clients must compare `ProtocolVersion` with the protocol they support before calling other
TUFReplay methods. A missing or different protocol version means the installed mod is incompatible.
The companion web UI asks the user to fully quit and restart ADOFAI so the startup updater can install
a compatible TUFReplay release. `ServerVersion` remains as a legacy compatibility field and is not the
TUFReplay namespace protocol version.

The timing dialog first offers compact global offset and microphone-gain controls without opening a level. Starting precise calibration transitions the same dialog into the existing calibration progress UI and opens the packaged level. Calibration is a transient session: its run and WAV are not written to the activity database. A successful clear exposes 2,048-bin native-input and microphone waveforms to the web editor. Every calibration starts from the raw, uncorrected microphone timing so repeated calibrations measure the absolute microphone delay instead of the residual after the previous correction. Preview playback runs in ADOFAI while the browser polls the game clock; the calibration's current offset and `-20 dB` to `+30 dB` microphone gain (`0 dB` by default, up to about `31.6x` before limiting) are applied to its preview, while the saved global values are applied to stored microphone replays. The true-peak limiter uses a `-0.3 dBFS` ceiling with a `30 ms` release so amplified playback remains protected while recovering quickly after transients.

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
