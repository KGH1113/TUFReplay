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

A new countdown starts a fresh payload when the previous capture already reached a terminal state, even if that attempt never obtained an activity draft. Final capture draining includes buffered inputs and the finishing hit in the terminal boundary without adding idle death-screen time. Render export can recover the specific older pre-start zero-terminal defect from its retained event timeline; it leaves the database unchanged and warns that pre-start key timestamps already saved as zero cannot be reconstructed. Other events after the recording boundary still fail validation.

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
- Optionally records H.264 camera video alongside each run and plays it inside the game during replay. Camera recording starts disabled. The companion header's camera button configures capture, crop, visibility, mirroring, synchronization, quality, storage budget, and retention. Position and size are adjusted directly in the game.
- Offers a Render action in each saved run's menu when TUFReplay-Renderer is installed. Its dialog configures the embedded OrbitRender engine's video, audio, display, and media options, including a 0–30 second wait after clear or death, and saves the finished video into a selected local folder. Mid-level/checkpoint recordings render from their recorded start tile; failures include the native death animation before the extra wait. Completion includes an action to open that folder. TUFReplay exports a validated neutral recording bundle, preserving the terminal outcome, signed countdown inputs, camera crop, mirror, position, timing, and microphone gain. The renderer reports overlay compatibility warnings. See [rendering integration](docs/replay-render-bundle.md).
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
- AdofaiIpc 0.4.1 or newer; a missing installation is attempted automatically
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

TUFHelperLite is optional. When installed, TUFReplay resolves its downloaded level paths to public TUF forum IDs; recording itself does not depend on it.

### Camera recording, live preview, and replay

Turn on **Camera** in the companion and choose a device. While enabled, capture stays open throughout the game, including between runs, so the operating system continues to show camera access. Only runs save video, and automatic recording must also be enabled. The first custom-level run after opening the game shows a uGUI camera check before play starts when both camera capture and **Show live camera** are enabled. If either option is off, play starts without the check; disabling either option while the check is open also resumes play. Skipping a hidden camera does not confirm a future run with live camera enabled. Its preview starts concealed behind **Click to reveal / 클릭해서 보기** and displays camera frames only after an explicit click or keyboard activation. Reopening the check or turning the camera off and on conceals it again. It offers camera capture, a live preview while playing, and starting without the camera; while live camera is enabled, the first frame must arrive before starting. The check appears once per game launch after confirmation. Calibration and TUFReplay-owned replays do not trigger it.

Capture also prepares one empty video writer in the background before a run. Input capture starts camera recording from countdown or checkpoint, using the native-input recorder's exact monotonic start boundary. That boundary is carried through the background queue and converted to the native host clock on macOS; encoder attachment time does not redefine it. The writer can reuse the latest complete frame if it is no more than 250 ms old and was captured at or after the input boundary. Earlier frames are excluded, and the first accepted frame keeps its real capture timestamp for synchronization. Idle frames are replaced in memory and never appended to the video; there is no continuously saved recording to trim afterward. Ending a run detaches its writer and prepares the next one while the previous MP4 finishes. Unused writers and their unique `.mp4.partial` destinations are removed when capture stops. Enabling the camera for the first time still requires device and writer preparation, and an unusually fast retry can wait for preparation already in progress.

The default **Compact / 용량 절약** profile fits within 640 × 480 at 30 fps and 600 kbps (about 4.5 MB/min). **Balanced / 균형** fits within 1280 × 720 at 30 fps and 1.2 Mbps (about 9 MB/min). **Quality / 품질** fits within 1920 × 1080 at 30 fps and 3 Mbps (about 22.5 MB/min). Recording preserves the camera's source aspect ratio without upscaling: for example, a 16:9 camera uses 640 × 360 in Compact, and a 4:3 camera uses 960 × 720 in Balanced. These are approximate sizes; actual bitrate and available camera modes vary. MP4 files and small synchronization sidecars live in `Mods/TUFReplay/Data/Webcam`, outside SQLite. The default total video limit is 512 MB, retention is seven days, and each recording is capped at 128 MB or the available budget. Video continues through both clear and failure screens until the user returns to the editor with Esc. A direct retry finishes the previous run's video before attaching a writer to the new input capture boundary; stopping the session also finalizes pending footage. Failure footage waits for the original activity transaction before being retained, and failure-tail timeline segments use real time at rate 1. Microphone audio remains a separate recording with its existing failure behavior. See [camera input boundaries and editor return](docs/camera-input-boundaries-2026-10-06.md).

The total storage limit removes the oldest camera videos when more space is needed; retention removes videos older than the selected number of days. Run history and input replays remain available. Pruning runs before capture and hourly afterward, and preserves footage currently being replayed. Deleting a run also deletes its camera footage. Camera retention applies independently of the microphone's permanent-keep option.

Camera files are retained only after their activity run commits, without making the next capture wait for the activity database. Releasing playback footage queues storage cleanup in the background, so the game thread does not wait for a retention scan or file deletion.

On macOS, the existing `TUFReplayMicrophoneCapture.app` helper keeps an AVFoundation capture session open and prepares AVAssetWriter before H.264 frames are appended during a run. Its video session starts at the first attached sample's presentation timestamp, and synchronization uses that sample's native host timestamp. It requests camera permission when capture is enabled and explains that access continues while the game is open. Allow **TUFReplay Microphone Capture** in **System Settings → Privacy & Security → Camera** if access was denied. The helper is built for both Apple silicon and Intel, with a macOS 12 deployment target. No FFmpeg installation is needed on macOS.

Windows keeps one FFmpeg DirectShow capture process open and drains frames continuously. A separate `libx264` encoder process is started ahead of the run and receives frames through a bounded queue only after attachment, without reopening the camera. Idle capture rotates two reusable YUV buffers so the latest complete frame remains available without copying every idle frame. When the Windows camera is first enabled or a render is first started without FFmpeg, the companion web dialog asks for consent before downloading the platform build into `Mods/TUFReplay/FFmpeg/<platform>/`. The web download center next to Camera shares the same consent, progress, cancellation and retry controls. Opening camera settings or the download center alone does not download anything. The Unity runtime bundle has no FFmpeg installation modal. Windows camera capture and TUFReplay-Renderer use this same installation; neither consults PATH, another mod's installation, or a manually configured executable. Existing executable-path settings are ignored. Camera capture on Linux is currently unsupported.

FFmpeg downloads are separate from release ZIPs and survive TUFReplay runtime updates. The installer keeps vendor notices and a receipt containing the provider URL, version/build configuration and executable SHA-256. It checks the Windows provider's archive checksum and verifies the executable before publishing the installation. Download, hashing, extraction and executable checks run off the game thread. A cancelled or failed download never becomes a ready installation. macOS camera capture continues to use the native helper; FFmpeg setup on macOS is needed only for rendering. The independent renderer requests installation through `media.ffmpeg.request`, polls `media.ffmpeg.status`, and can dismiss a pending consent prompt with `media.ffmpeg.cancel-pending` over ADOFAI IPC. The web UI approves through `media.ffmpeg.confirm`; installation resumes the waiting camera or render automatically.

TUFReplay alone exposes `downloads.status` and `downloads.renderer.request|confirm|cancel`. The icon-only download center next to Camera lists installed components without nested cards. Installation consent, progress, errors and restart instructions appear in a separate web dialog after the dropdown closes. Requests from first-time camera activation or rendering open that same dialog automatically. The center can install the independent TUFReplay-Renderer mod from its most recently published official GitHub release, including betas, after explicit consent. That release must contain `TUFReplay-Renderer.zip` and its generated `TUFReplay-Renderer.download.json` verification manifest. The installer checks package size, SHA-256, mod identity and version, rejects unsafe ZIP paths and user-data entries, and publishes a staged folder atomically. It never overwrites an existing Renderer folder or hot-loads the DLL; fully restart ADOFAI and enable the mod after installation. A missing release or incomplete existing folder has a specific recovery message. No release lookup or download happens until installation is approved.

The Windows encoder reuses pooled YUV frame buffers across runs and limits both filter workers and encoder workers to two threads. RGB preview conversion/rendering runs only while the game overlay or camera check consumes it; the first frame is always published for readiness. Busy preview writes are skipped by game-thread readers while the last complete texture remains displayed. These optimizations preserve capture rate, aspect ratio, colors, and recording presets.

Both backends share only the latest, uncompressed RGBA preview, fitting within 960 × 720 at up to 30 fps with the source aspect ratio preserved (960 × 540 for 16:9). Preview quality is independent of the recording preset: capture retains enough source resolution for the preview where the device supports it, while the encoder fits the saved video within the selected preset's budget. A fixed-size memory mapping uses at most about 2.64 MiB and carries the actual frame dimensions, including portrait cameras. macOS uses CPU Lanczos downscaling on a separate worker so preview rendering does not wait for the game's GPU or block capture and encoding. The worker holds only its current frame and the latest pending frame, replacing stale pending frames when it falls behind. It renders into a reusable private buffer before briefly copying the completed frame into shared memory. It converts NV12's video range and YCbCr matrix while preserving the camera's non-linear RGB values, avoiding an additional Rec.709-to-sRGB transfer that would brighten midtones. Windows uses area averaging with cached sampling coefficients. Preview pixels use Unity's bottom-up row order; the mirror setting affects only the horizontal direction. Unity copies each complete frame with one bulk memory copy, avoiding Mono's per-byte `ReadArray<byte>` path. It uploads a new preview frame only when the live overlay or camera check is visible and keeps the last complete texture while the next frame is being written, including resolution changes. No preview images accumulate on disk, and capture and encoding stay off the game thread. Increasing preview resolution does not increase recorded video storage.

For synthetic performance checks without opening the game or camera, run `./scripts/run.sh camera-copy-bench 100` to compare both memory-copy paths in standalone Mono. It uses an installed Mono toolchain, including Unity Editor's bundled toolchain when available; this is not a measurement inside the running game's Mono. Run `TUFREPLAY_CAMERA_PREVIEW_BENCHMARK=1 ./scripts/run.sh mac-helper` to also report preview render timings during the helper self-test.

`./scripts/run.sh mod-check` validates native writer preparation, attachment, MP4 decoding, idle-writer cleanup, bounded frame reuse, and capture-clock alignment. To additionally exercise the FFmpeg recording writer with synthetic frames, set `TUFREPLAY_CAMERA_TEST_FFMPEG` to a local FFmpeg executable when running `mod-check`. This optional check uses no camera and decodes the resulting MP4 to verify that only the twelve submitted frames were saved.

Camera diagnostics are enabled automatically. The game's `Player.log` includes `[Camera/Diagnostics]` JSON records for capture operations, queue/command timing, helper launch and handshake, the selected device and recording profile, preview readiness, storage, game-clock anchors, and full managed exceptions. On macOS, the helper also writes timestamped JSON lines to `~/Library/Logs/TUFReplay/camera-helper-*.jsonl`. The handshake records the exact helper log path and PID in `Player.log`. Native logs include permission decisions, device/format selection, session notifications, NSError domains/codes and underlying errors, encoder failures, and a health snapshot every five seconds while armed. The snapshot separates received/dropped/encoded frames, timestamp delay, preview-worker progress, and the shared-frame sequence and demand flag. macOS interruption notifications preserve the available notification user information rather than assuming an iOS-only interruption reason.

Writer diagnostics include `recording.prepare.complete`, preparation failures, and standby/preparing state. `capture.storage.reserved.startTimestampTicks` records the requested input boundary. macOS `recording.writer.started` reports whether a prepared writer was used and the attachment time; `recording.first-frame` reports whether it reused the latest sample and its delay relative to the requested boundary. New run recordings reject frames from before that boundary. Replay diagnostics separately report decoder preparation, seek completion, the first decoded frame, and the first requested display, allowing capture delay and playback delay to be compared. See [camera writer preparation and replay startup](docs/camera-warm-recording-2026-10-04.md).

The game also emits `render.performance` records every five seconds, including when the camera is off for a baseline. Schema version 2 identifies the renderer as `ugui` and measures game-thread elapsed time for camera surface/cursor updates, uGUI pointer handling, their sum per completed game frame, preview reads and successful frame copies, and texture loading/apply. Each timing includes count, total, mean, and maximum milliseconds. Records include observed game FPS from monotonic frame intervals, camera/focus/gameplay state counts, frame limit, VSync, upload count, and dimensions. Per-frame counters do not allocate; snapshot serialization and Player.log writes run on a separate worker. These timings measure CPU-side elapsed work and submission or waits, not GPU execution time or Unity's entire Canvas processing; nested stages must not be added to their parent timing. See [camera runtime fixes and profiling](docs/camera-runtime-fixes-and-profiling-2026-10-04.md) for the fields and comparison workflow.

Diagnostic file encoding and writes run on a separate helper queue; frame callbacks only update counters and emit first-occurrence or state-transition events. Each native log rotates at 2 MiB, keeps one previous file, and prunes the directory to at most 12 camera diagnostic files. Camera pixels, recorded media, and handshake tokens are not logged. Synthetic self-test entries carry `selfTest: true`. See [collecting camera-unavailable diagnostics](docs/camera-unavailable-diagnostics-2026-10-03.md) for log locations and installation steps.

Synchronization maps the first encoded frame's native timestamp to the same monotonic input clock used by microphone capture. Runs ending before a game-clock anchor is available, or with an invalid capture timestamp, discard their unsynchronized video without disarming the camera or showing an unavailable warning; `capture.recording.discarded` records the reason. Pause gaps and gameplay-rate changes are stored as sparse timeline segments. Unity's `VideoPlayer` follows replay time for pause, resume, retry, seek, speed changes, and the post-clear tail. An additional −1000…+1000 ms correction compensates for device latency: positive values display the video earlier, matching microphone offset settings. Pause near a visible key press to compare the image with replay input.

The camera overlay uses a persistent uGUI Canvas and RawImage for both live gameplay and replay video; it has no OnGUI renderer or IMGUI mouse handling. The existing texture is reused as new frames arrive, and layout, UVs, and texture bindings change only when their values change. Hidden camera canvases and raycasters are inactive. It defaults to the bottom-right corner at 22% of the game-window width. Live gameplay visibility and replay visibility are separate settings. Drag the image in the game during live play or replay to move it; its center uses a move cursor. Drag any edge or corner to resize while preserving the cropped image's aspect ratio; the cursor shows the resize direction. uGUI retains pointer capture outside the image during a drag. Positions can extend beyond the screen with no safe-area margin, and position and size are saved when a gesture finishes. The mod settings offer **Reset camera position** to recover an offscreen camera. The companion no longer includes placement, corner presets, or size controls.

The companion camera dialog includes a live source preview, concealed behind **Click to reveal / 클릭해서 보기** until explicitly opened. Drag the crop rectangle's corners to change the visible region or its center to move that region; the excluded area is shaded. Arrow keys also adjust the focused crop control, with Shift for larger steps. Crop coordinates use the original source, so mirroring does not change the selected region. The crop is applied to live gameplay, replay video, and the first-run camera check; recorded MP4 files keep the complete source frame so the crop can be changed later.

The companion uses the browser's `getUserMedia` API to open its own camera preview after **Click to reveal**. It displays the `MediaStream` directly in a muted video element, with the camera's aspect ratio preserved. Camera access requires permission for the companion's browser origin. Closing or hiding the preview, hiding the tab, turning the camera off, or changing devices stops its media tracks. The browser preview does not transfer image frames through IPC or encode preview images. The game continues to manage its own capture session and run recordings.

When replay camera visibility is enabled, decoder preparation starts as soon as the recording is acquired, before gameplay playback begins. The player can seek to the first frame while the replay clock is still before the video's start, keeping the image concealed until its timestamp is reached. Disabling replay camera visibility stops preparation and decoding; showing it again prepares the decoder and seeks to the current replay clock before displaying a frame.

### Optional mod integration

Other mods can detect a TUFReplay-owned replay operation without taking a compile-time dependency on TUFReplay. Resolve the public type `TUFReplay.ReplayRuntime` from the loaded TUFReplay assembly and read its static `IsPlaybackActive` property. The property is true throughout replay preparation, level loading, playback, and the return to the editor. `ReplayRuntime.ApiVersion` is `1` for this contract.

Reflection consumers should cache the resolved type and property getter, query the value only at relevant lifecycle boundaries, and treat a missing type, property, or assembly as an inactive replay.

## Repository Layout

- `TUFReplay/`: UnityModManager mod source, organized first by feature and then by concrete role.
  - `Activity/`: `Models`, `Queries`, `Tracking`, `Repositories`, `Migrations`, `Charts`, and `Ipc`.
  - `Replay/`: `Models`, `Sessions`, `Preparation`, `Transport`, `Playback`, `NativeInput`, `Levels`, `Timeline`, `Patches`, and `Ipc`.
  - `Recording/`: `Models`, `Sessions`, `Input`, `Activity`, `Microphone`, and `Patches`.
  - `Microphone/`: `Models`, `Devices`, `Capture`, `Playback`, `Processing`, `Timing`, `Recording`, `Repositories`, and `Ipc`.
  - `Webcam/`: `Models`, `Capture`, `Timing`, `Recording`, `Repositories`, `Playback`, and `Ipc`.
  - `Calibration/`: `Models`, `Sessions`, `Analysis`, `Playback`, `Levels`, and `Ipc`.
  - `Composition/`: the mod composition root and feature registry. This is separate from the fixed launcher assembly in `TUFReplay.Bootstrap/`.
  - `Shared/`: database, IPC, settings, native-input, and Unity primitives shared by multiple features.
- `TUFReplay.Bootstrap/`: fixed launcher that selects and loads a versioned TUFReplay runtime.
- `TUFReplay.UpdateEngine/`: versioned update and package installation engine.
- `TUFReplay.Tests/`: executable C# test harness, grouped into activity/database, microphone/calibration, webcam, and replay/native-input suites.
- `TUFReplay.UpdateTests/`: updater test suite linked into the main C# test harness.
- `TUFReplay.Unity/`: Unity 6.3 project for the replay timeline and first-run camera-check prefabs, Canvas graphics, shader, and platform AssetBundle builder. **Tools → TUFReplay → Preview Camera Setup Modal** opens the saved `Assets/Scenes/CameraSetupPreview.unity` scene. Its editor-only driver restores the dialog after scene reloads, recompilation, and Play mode changes. **Build Runtime UI Bundles** includes the dialog in all platform bundles and returns to the previously open scenes afterward.
- `web/`: Bun/Vite companion web UI, managed as a workspace package.
- `TUFReplay.MicrophoneCapture.Mac/`: Xcode project for the AVFoundation helper used for macOS microphone/camera permission and capture.
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
- On macOS, builds the helper's Xcode Release scheme, verifies its self-test and universal arm64/x86_64 executable, ad-hoc signs it, and installs the app with microphone and camera usage descriptions. Synthetic webcam self-tests encode and decode H.264 without opening a camera.
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

`unity-ui` rebuilds the replay timeline, camera setup, and generic runtime notification prefabs with Unity 6000.3.10f1 and writes `tufreplay_ui.bundle` files to `TUFReplay/Assets/mac`, `win`, and `linux`. The bundle contains the TUFHelperLite-style linear transport panel, first-run camera check, uGUI toast/persistent-error UI, and MapleStory TMP font assets, without redistributing extracted ADOFAI images.

The entry point dispatches to workflows, workflows only sequence tasks, and tasks use the shared context, validation, dependency, and artifact libraries. Individual task scripts under `scripts/tasks` can also be run directly while diagnosing one build stage.

Beta releases use the same two assets and must be marked as a prerelease on GitHub. Version 0.2.0-beta.1 introduces replay engine `tufreplay.replay.v2`, payload format 1, and a one-time web notice for records from the previous engine. The new activity database uses application ID `0x54554652` and schema version 1, while gameplay identity uses hash v4. Schema v15 activity and microphone records are imported while the source is preserved as `tufreplay.pre-0.2.sqlite`; replay payloads recorded by the previous engine are intentionally not imported. Schema v14 and earlier databases log a warning and are replaced with a fresh current database. App SemVer, activity schema, replay engine, replay payload, and gameplay hash versions are independent compatibility boundaries.

## Web Development

Install the Bun workspace dependencies from the repository root:

```bash
bun install
```

Run the companion web UI:

```bash
VITE_WEB_ADOFAI_EMBED_URL=http://127.0.0.1:5173/embed/chart ./scripts/run.sh web-dev
```

The web UI bundles English and Korean translation resources under `web/src/i18n/locales`. The language menu stores the explicit selection in `localStorage`; without a saved selection, Korean browser locales use Korean and all other locales use English.

The run card's Render action opens the same original-or-matching-level chooser used for replay. Choosing another file verifies its gameplay with ADOFAI before opening the video settings. Render selection restores the game screen after verification instead of holding it black for immediate replay. The selected path belongs only to this render and is revalidated during bundle export; it is never saved as the default for another run. Selecting a level does not start replay playback.

Video settings start with **Recommended** quality: Lowest (720p30), Low (720p60), Medium (1080p60), High (1440p60), Highest (2160p60), or Extreme (2160p120). The independent Renderer reports the ADOFAI computer's CPU, memory, GPU and maximum texture size, and checks matching H.264 hardware encoders with two synthetic frames on a background worker. A passing hardware encoder is preferred; otherwise presets use software encoding. The suggested default is a conservative CPU/memory/GPU heuristic rather than a level benchmark, and Extreme always requires an explicit selection. The browser's own hardware is never used. First-use FFmpeg installation refreshes the recommendation before bundle export. Older Renderers that omit system information use conservative software defaults. **Advanced** exposes the existing individual video, codec, encoder and game-display options; entering it copies the currently shown recommendation, and quality changes retain the user's save location, media selections, audio gain and ending delay.

**Remember these settings** also saves the video settings mode and quality. The automatic quality follows the next system recommendation; a selected tier or Advanced mode is restored. Clients that omit the optional preference fields retain the saved preference.

New runs refresh the current day's list in the background while retaining the selected tile/run, the open run sidebar, and the chart's current view. When viewing cards below the top of the list, new cards preserve the visible card's scroll position. Refresh failures keep the existing chart and list available with a retry action; changing the day or gameplay revision resets the selection for that scope.

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
./scripts/run.sh web-check
./scripts/run.sh web-format
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

Format or check changed C# sources through the workflow entry point (restores the repository-local CSharpier tool automatically):

```bash
./scripts/run.sh mod-format
./scripts/run.sh mod-format check
```

Format or check the web workspace:

```bash
./scripts/run.sh web-format
./scripts/run.sh web-check
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
- `replay.render-bundle.export` (`runId`, optional `levelPath`, `includeWebcam`, and `includeMicrophone`; starts background export after a game-thread settings snapshot)
- `replay.render-bundle.status.get` (`jobId`; returns progress, terminal state, or the prepared `manifestPath`)
- `replay.render-bundle.cancel` (`jobId`; cancels an active preparation job and removes its partial files)
- `replay.level-file.pick` (waits for selection and in-game gameplay-hash verification, then returns `selected`, `mismatch`, `cancelled`, or `error`; optional `purpose: "render"` restores the game screen after verification, while omitted or `"replay"` holds it for replay startup)
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
- `webcam.settings.get` (`refreshDevices: true` forces camera discovery; ordinary polling uses a 30-second device cache)
- `webcam.settings.update` (partial camelCase settings: `enabled`, `deviceId`, `quality`, `storageLimitMb`, `retentionDays`, `offsetMs`, `playbackVisible`, `liveVisible`, `mirror`, `crop: {x, y, width, height}`, and legacy `overlayX`, `overlayY`, `overlayWidth`; crop is an atomic normalized source rectangle, with each dimension at least 0.05. Capture settings are locked during recording/finalization, while display and synchronization remain adjustable.)

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
