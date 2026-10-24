<div align="center">

![TUFReplay](https://capsule-render.vercel.app/api?type=waving&height=220&color=0:050505,45:101827,100:5eead4&text=TUFReplay&fontColor=e6fffb&fontAlignY=38&desc=Clear,%20Record,%20Edit,%20Upload,%20Submit,%20Wait%20->%20Clear,%20Submit%20&descAlignY=58&animation=fadeIn)

[![Runtime](https://img.shields.io/badge/runtime-ADOFAI%20%2F%20Unity-111827?style=for-the-badge&logo=unity&logoColor=white)](https://store.steampowered.com/app/977950/A_Dance_of_Fire_and_Ice/)
[![Mod Loader](https://img.shields.io/badge/mod%20loader-UnityModManager-7c3aed?style=for-the-badge)](https://www.nexusmods.com/site/mods/21)
[![Patching](https://img.shields.io/badge/patching-Harmony-f43f5e?style=for-the-badge)](https://github.com/pardeike/Harmony)
[![Build](https://img.shields.io/badge/build-.NET%20SDK-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Target](https://img.shields.io/badge/target-netstandard2.1-2563eb?style=for-the-badge)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Database](https://img.shields.io/badge/database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://sqlite.org/)
[![API](https://img.shields.io/badge/api-AdofaiIpc-f97316?style=for-the-badge)](#typed-adofaiipc-api)

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
- Captures the gameplay hit limit (None, PerfectsOnly, or PurePerfectOnly) with new replay artifacts, restores it during in-game playback, and includes it in render bundles for renderers that support the setting.
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
- Optionally records H.264 camera video alongside each run and plays it inside the game during replay. Camera recording starts disabled. Its switch sits beside the camera dialog title. Camera selection uses the companion's shadcn Select. Setting help is available beside each section title on hover or keyboard focus; sync correction expands below retention without resizing the dialog, whose content scrolls within a fixed viewport. The companion header's camera button configures capture, crop, visibility, mirroring, synchronization, quality, per-recording size limits, and retention. Position and size are adjusted directly in the game.
- On `dev`, each saved run's Render action shows a coming-soon notice. TUFReplay-Renderer is also marked as coming soon in the download center; renderer installation actions and automatic consent prompts are unavailable. FFmpeg installation for camera recording remains available. This branch retains its existing render bundle API, but does not enable the separate rendering workflow. See [rendering integration](docs/replay-render-bundle.md) for background on that work.
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
- ADOFAI-IPC 2.1.0 bundled DLLs (included; no separate IPC mod installation)
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

TUFHelperLite is optional. When installed, TUFReplay resolves its downloaded level paths to public TUF forum IDs; recording itself does not depend on it.

### Camera recording, live preview, and replay

Turn on **Camera** in the companion and choose a device. While enabled, capture stays open throughout the game, including between runs, so the operating system continues to show camera access. Only runs save video, and automatic recording must also be enabled. The first custom-level run after opening the game shows a uGUI camera check before play starts when both camera capture and **Show live camera** are enabled. If either option is off, play starts without the check; disabling either option while the check is open also resumes play. Skipping a hidden camera does not confirm a future run with live camera enabled. Its preview starts concealed behind **Click to reveal / 클릭해서 보기** and displays camera frames only after an explicit click or keyboard activation. Reopening the check or turning the camera off and on conceals it again. It offers camera capture, a live preview while playing, and starting without the camera; while live camera is enabled, the first frame must arrive before starting. The check appears once per game launch after confirmation. Calibration and TUFReplay-owned replays do not trigger it.

Capture also prepares one empty video writer in the background before a run. Input capture starts camera recording from countdown or checkpoint, using the native-input recorder's exact monotonic start boundary. That boundary is carried through the background queue and converted to the native host clock on macOS; encoder attachment time does not redefine it. The writer can reuse the latest complete frame if it is no more than 250 ms old and was captured at or after the input boundary. Earlier frames are excluded, and the first accepted frame keeps its real capture timestamp for synchronization. Idle frames are replaced in memory and never appended to the video; there is no continuously saved recording to trim afterward. Ending a run detaches its writer and prepares the next one while the previous MP4 finishes. Unused writers and their unique `.mp4.partial` destinations are removed when capture stops. Enabling the camera for the first time still requires device and writer preparation, and an unusually fast retry can wait for preparation already in progress.

The default **Balanced / 균형** profile fits within 1280 × 720 at 30 fps and 1.2 Mbps (about 9 MB/min). **Compact / 용량 절약** fits within 640 × 480 at 30 fps and 600 kbps (about 4.5 MB/min). **Quality / 고화질** fits within 1920 × 1080 at 60 fps and 6 Mbps (about 45 MB/min). Existing saved quality selections are preserved. Recording preserves the camera's source aspect ratio without upscaling: for example, a 16:9 camera uses 640 × 360 in Compact, and a 4:3 camera uses 960 × 720 in Balanced. These are approximate sizes; actual bitrate and available camera modes vary. MP4 files and small synchronization sidecars live in `Mods/TUFReplay/Data/Webcam`, outside SQLite. Each recording has its own configurable 64–8192 MB limit, defaulting to 128 MB. There is no aggregate video storage limit. Reaching the limit finalizes the footage recorded so far while microphone and replay capture continue. Retention defaults to seven days; the slider's last position keeps videos forever. Video continues through both clear and failure screens until the user returns to the editor with Esc. A direct retry finishes the previous run's video before attaching a writer to the new input capture boundary; stopping the session also finalizes pending footage. Failure footage waits for the original activity transaction before being retained, and failure-tail timeline segments use real time at rate 1. Microphone audio remains a separate recording with its existing failure behavior. See [camera input boundaries and editor return](docs/camera-input-boundaries-2026-10-06.md).

Retention removes camera videos older than the selected number of days, except videos individually marked for permanent retention. Selecting **Forever / 영구 보관** disables age pruning. Run history and input replays remain available. Cleanup runs before capture and hourly afterward, and preserves footage currently being replayed. Deleting a run also deletes its camera footage. Each run's menu uses **Microphone and Camera / 마이크 및 카메라**, regardless of which recordings are saved, and disables the item when neither file is available. The recordings dialog disables tabs without a saved file and selects the remaining tab after a recording is deleted. Each tab lets you review the recording's size and retention, keep it forever, export a microphone WAV or camera MP4, or delete the file independently. Deletion needs confirmation and is unavailable while that run is replaying; it keeps the run and the other recording.

Camera files are retained only after their activity run commits, without making the next capture wait for the activity database. Releasing playback footage queues storage cleanup in the background, so the game thread does not wait for a retention scan or file deletion.

On macOS, the existing `TUFReplayMicrophoneCapture.app` helper keeps an AVFoundation capture session open and prepares AVAssetWriter before H.264 frames are appended during a run. Its video session starts at the first attached sample's presentation timestamp, and synchronization uses that sample's native host timestamp. It requests camera permission when capture is enabled and explains that access continues while the game is open. Allow **TUFReplay Microphone Capture** in **System Settings → Privacy & Security → Camera** if access was denied. The helper is built for both Apple silicon and Intel, with a macOS 12 deployment target. No FFmpeg installation is needed on macOS.

Windows keeps one FFmpeg DirectShow capture process open and drains frames continuously. It reads the camera's supported modes before opening it, preserves fractional capture rates, and can use 1080p30 when the Quality profile's 60 fps is unavailable. Equivalent MJPEG modes are preferred to reduce USB bandwidth. Failed advertised modes are retried with bounded startup waits; the final attempt lets the device choose its default.

Windows probes NVIDIA NVENC, Intel Quick Sync, AMD AMF, then software H.264 by actually encoding test frames at the selected recording size and nominal frame rate. Successful preparation is cached. Recording uses low-latency settings without B-frames or lookahead; software fallback uses `libx264 ultrafast` with two threads. A writer is prepared before the run, and its encoder process starts on the background writer when the first eligible frame arrives, so the run's selected file-size limit can be applied. Idle writers do not hold GPU encoder sessions. If hardware encoding fails after a successful probe, subsequent runs use software until the camera is rearmed.

Capture, preview and recording share a fixed pool of eight YUV frames allocated while arming (about 25 MB at 1080p), without copying a full frame to enqueue it. The recording queue retains at most three pending frames; overload replaces stale pending frames instead of failing the whole recording. Each retained frame carries its original capture timestamp through a streaming Matroska pipe to a variable-frame-rate MP4, preserving gaps and replay synchronization. Preview conversion runs independently, at most 30 times per second, with one pending frame and native bulk copies into shared memory. Recording is scaled once in the capture process. Windows capture/encoder processes and preview conversion use below-normal CPU priority to favor gameplay under contention. This still transfers raw frames through CPU memory and process pipes; hardware encoding does not make the entire capture path GPU-only. `[Webcam]` logs report the chosen capture mode/encoder, probe failures, and encoded/dropped frame counts. Size-limited recordings read their final duration from the saved MP4 video header rather than encoder progress, which can include packets the muxer did not save.

When the Windows camera is first enabled or a render is first started without FFmpeg, the companion web dialog asks for consent before downloading the platform build into `Mods/TUFReplay/FFmpeg/<platform>/`. The web download center next to Camera shares the same consent, progress, cancellation and retry controls. Opening camera settings or the download center alone does not download anything. The Unity runtime bundle has no FFmpeg installation modal. Windows camera capture and TUFReplay-Renderer use this same installation; neither consults PATH, another mod's installation, or a manually configured executable. Existing executable-path settings are ignored. Camera capture on Linux is currently unsupported.

FFmpeg downloads are separate from release ZIPs and survive TUFReplay runtime updates. The installer keeps vendor notices and a receipt containing the provider URL, version/build configuration and executable SHA-256. It checks the Windows provider's archive checksum and verifies the executable before publishing the installation. Download, hashing, extraction and executable checks run off the game thread. A cancelled or failed download never becomes a ready installation. macOS camera capture continues to use the native helper; FFmpeg setup on macOS is needed only for rendering. The independent renderer sends `media.ffmpeg.request` through an application-owned recorder port backed by the IPC local peer router and waits for `media.ffmpeg.state.changed`. It releases its own pending request with `media.ffmpeg.release`; camera and web request owners remain independent. The web download center sends `downloads.ffmpeg.confirm`, and pushed installation state resumes the waiting camera or render automatically. No loopback HTTP client or installation status polling is used.

TUFReplay alone exposes `downloads.status` and `downloads.renderer.request|confirm|cancel`. The icon-only download center next to Camera lists installed components without nested cards. Installation consent, progress, errors and restart instructions appear in a separate web dialog after the dropdown closes. Requests from first-time camera activation or rendering open that same dialog automatically. The center can install the independent TUFReplay-Renderer mod from its most recently published official GitHub release, including betas, after explicit consent. That release must contain `TUFReplay-Renderer.zip` and its generated `TUFReplay-Renderer.download.json` verification manifest. The installer checks package size, SHA-256, mod identity and version, rejects unsafe ZIP paths and user-data entries, and publishes a staged folder atomically. It never overwrites an existing Renderer folder or hot-loads the DLL; fully restart ADOFAI and enable the mod after installation. A missing release or incomplete existing folder has a specific recovery message. No release lookup or download happens until installation is approved.

The Windows encoder reuses pooled YUV frame buffers across runs and limits both filter workers and encoder workers to two threads. RGB preview conversion/rendering runs only while the game overlay or camera check consumes it; the first frame is always published for readiness. Busy preview writes are skipped by game-thread readers while the last complete texture remains displayed. These optimizations preserve capture rate, aspect ratio, colors, and recording presets.

Both backends share only the latest, uncompressed RGBA preview, fitting within 960 × 720 at up to 30 fps with the source aspect ratio preserved (960 × 540 for 16:9). Preview quality is independent of the recording preset: capture retains enough source resolution for the preview where the device supports it, while the encoder fits the saved video within the selected preset's budget. A fixed-size memory mapping uses at most about 2.64 MiB and carries the actual frame dimensions, including portrait cameras. macOS uses CPU Lanczos downscaling on a separate worker so preview rendering does not wait for the game's GPU or block capture and encoding. The worker holds only its current frame and the latest pending frame, replacing stale pending frames when it falls behind. It renders into a reusable private buffer before briefly copying the completed frame into shared memory. It converts NV12's video range and YCbCr matrix while preserving the camera's non-linear RGB values, avoiding an additional Rec.709-to-sRGB transfer that would brighten midtones. Windows uses area averaging with cached sampling coefficients. Preview pixels use Unity's bottom-up row order; the mirror setting affects only the horizontal direction. Unity copies each complete frame with one bulk memory copy, avoiding Mono's per-byte `ReadArray<byte>` path. It uploads a new preview frame only when the live overlay or camera check is visible and keeps the last complete texture while the next frame is being written, including resolution changes. No preview images accumulate on disk, and capture and encoding stay off the game thread. Increasing preview resolution does not increase recorded video storage.

For synthetic performance checks without opening the game or camera, run `./scripts/run.sh camera-copy-bench 100` to compare mapped-buffer `ReadArray`/`WriteArray` against native bulk reads/writes in standalone Mono. It uses an installed Mono toolchain, including Unity Editor's bundled toolchain when available; this is not a measurement inside the running game's Mono. `./scripts/run.sh mod-check` also exercises frame ownership, overload, device/encoder selection policies, and synthetic MP4 encoding when FFmpeg is available in the test environment (`TUFREPLAY_TEST_FFMPEG` can select the test executable). Run `TUFREPLAY_CAMERA_PREVIEW_BENCHMARK=1 ./scripts/run.sh mac-helper` to also report preview render timings during the helper self-test.

`./scripts/run.sh mod-check` validates native writer preparation, attachment, MP4 decoding, idle-writer cleanup, bounded frame reuse, and capture-clock alignment. To additionally exercise the FFmpeg recording writer with synthetic frames, set `TUFREPLAY_CAMERA_TEST_FFMPEG` to a local FFmpeg executable when running `mod-check`. This optional check uses no camera and decodes the resulting MP4 to verify that only the twelve submitted frames were saved.

Version 1.0.1 removes automatic camera and input measurement logs, per-frame rendering/engine profiling, input lateness histograms, and the macOS helper JSONL log writer. Camera and replay failures still reach the existing user notifications and error logs. Capture timestamps, recording boundaries, prepared writers, and replay synchronization remain active. Historical diagnostic documents describe earlier versions.

Synchronization maps the first encoded frame's native timestamp to the same monotonic input clock used by microphone capture. Runs ending before a game-clock anchor is available, or with an invalid capture timestamp, discard their unsynchronized video without disarming the camera or showing an unavailable warning. Pause gaps and gameplay-rate changes are stored as sparse timeline segments. Unity's `VideoPlayer` follows replay time for pause, resume, retry, seek, speed changes, and the post-clear tail. An additional −1000…+1000 ms correction compensates for device latency: positive values display the video earlier, matching microphone offset settings. Pause near a visible key press to compare the image with replay input.

The camera overlay uses a persistent uGUI Canvas and RawImage for both live gameplay and replay video; it has no OnGUI renderer or IMGUI mouse handling. The existing texture is reused as new frames arrive, and layout, UVs, and texture bindings change only when their values change. Hidden camera canvases and raycasters are inactive. It defaults to the bottom-right corner at 22% of the game-window width. Live gameplay visibility and replay visibility are separate settings. Drag the image in the game during live play or replay to move it; its center uses a move cursor. Drag any edge or corner to resize while preserving the cropped image's aspect ratio; the cursor shows the resize direction. uGUI retains pointer capture outside the image during a drag. Positions can extend beyond the screen with no safe-area margin, and position and size are saved when a gesture finishes. The companion no longer includes placement, corner presets, or size controls.

The companion camera dialog includes a live source preview, concealed behind **Click to reveal / 클릭해서 보기** until explicitly opened. Drag the crop rectangle's corners to change both dimensions, its edges to crop along one axis, or its center to move that region; the excluded area is shaded. Arrow keys also adjust the focused crop control, with Shift for larger steps. Crop coordinates use the original source, so mirroring does not change the selected region. The crop is applied to live gameplay, replay video, and the first-run camera check; recorded MP4 files keep the complete source frame so the crop can be changed later.

The companion shows frames from the game's existing camera after **Click to reveal**. It never opens a second camera device or asks for browser camera permission. The generated `readWebcamPreview({ deviceId })` method returns `null` while warming up or `{ width, height, file }` with a single-use `ReceivedFile`; the SDK streams frame bytes outside control messages. A sequential, cancellable preview session requests at most eight BMP frames per second, bounded to 640 × 480 with the source aspect preserved. Copying and bitmap conversion run on IPC workers, independent of the game's preview reader. Hide, close, camera change, tab hiding, page exit, connection loss, and unmount stop requests and release image URLs; the capture-side preview demand expires within one second. Camera capture and run recording stay active. Horizontal and vertical flip buttons beside the crop apply to the web preview, game overlays, and the updated Renderer composition, while original footage and source crop coordinates remain unchanged. See [shared camera preview and recording recovery](docs/camera-shared-preview-2026-10-06.md).

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
- `ipc/main.tsp`: source of the recorder's typed C# and TS IPC contract.
- `web/`: Bun/Vite companion web UI, including the pinned generated `@tuf-replay/ipc` package.
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

TUFReplay bundles Contracts and Loader at the mod root and the runtime/manifest under `ipc/`. Builds verify the committed `vendor/adofai-ipc-runtime/SHA256SUMS`; the game does not download or enable an IPC dependency automatically. The loader selects one compatible process runtime, and disabling TUFReplay disposes its typed mod registration, app sessions, and pending operations. The TUFReplay updater validates and copies its candidate bundle before loading its own payload; an already selected IPC host stays active until game restart.

The single `TUFReplay.zip` supports clean manual installs and automatic upgrades from the actually published v0.2.0-beta.4 package. Its versioned runtime retains the genuine v1 control files for old updater validation and stages the SDK plus a distinct `Runtime/launchers/<version>/TUFReplay.SdkLauncher.dll` before entering SDK code. Once initialization succeeds, it commits the root runtime state and entrypoint; future launches never enter the old IPC installer. Launcher updates select a new versioned path instead of replacing the currently loaded DLL. Failed downloads and incomplete bundles preserve the previous runtime and entrypoint. Keep this compatibility payload for the next several releases so users can skip the first SDK release. Settings, `UpdateSettings.json`, and `Data/` stay at the mod root.

Version 1.0.0 is the first stable GitHub Latest release. This also reaches existing users with beta updates disabled: earlier published packages were prereleases, while their default updater uses the stable Latest download URLs. The separate IPC 0.4.2 transition release silently retires the standalone IPC mod only when installed consumers have committed the SDK entrypoints. Actual game initialization, load order, and Windows file locking still require the supplied game E2E scenarios.

While the SDK dev-to-main pull request is awaiting review, open the [SDK companion on dev](https://tufreplay-dev.impl1113.dev/) with 1.0.0. Its `/adofai-ipc.json` reports SDK 2.1.0 and protocol 3; the mod already permits that origin. The main companion remains on the old contract until that pull request is merged. No mod setting change is needed to use the dev URL.

Starting with 1.0.1, the Unity Mod Manager GUI contains only the `Receive beta updates` toggle for standard builds. Camera settings remain in the web companion. Auto-submission builds use their isolated update feed and do not expose this toggle. Beta updates are disabled by default and saved to `UpdateSettings.json`; changes apply on the next game launch. The beta channel selects the highest compatible stable or prerelease SemVer from GitHub Releases. Disabling the channel never automatically downgrades an installed beta build.

Important environment variables:

- `ADOFAI_DIR`: ADOFAI install directory.
- `ADOFAI_MODS_DIR`: ADOFAI Mods directory.
- `ADOFAI_MANAGED`: Unity managed assembly directory.
- `DOTNET_EXE`: .NET SDK executable.
- `ADOFAI_IPC_BUNDLE`: optional override for the committed IPC 2.1.0 bundle directory.
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

The companion keeps discovering the game when opened before ADOFAI starts and reconnects after a game restart. WebSocket connectivity and TUFReplay readiness are separate: activity reads recover on recorder readiness, including after a previous readiness timeout, while retained records stay read-only during disconnection. The SDK validates idle connections with ping/pong and releases the old page's socket on navigation; focus, visibility return, network return and restored pages check the retained session. Reconnection never replays user actions. Browser storage and unrelated site caches are preserved.

Discovery uses an eight-second handshake allowance and retries the last successful port first. Chromium can delay new WebSocket handshakes by up to five seconds after failures in a renderer process; the previous 500 ms deadline could repeatedly abort those delayed attempts, including after a page reload in the same process.

On this branch, the run card's Render action opens a coming-soon notice. Renderer video settings and installation remain inactive. The IPC migration preserves the existing bundle preparation/cancellation contract without enabling the separate renderer workflow.

New runs refresh the current day's list in the background while retaining the selected tile/run, the open run sidebar, and the chart's current view. When viewing cards below the top of the list, new cards preserve the visible card's scroll position. Refresh failures keep the existing chart and list available with a retry action; changing the day or gameplay revision resets the selection for that scope.

`VITE_WEB_ADOFAI_EMBED_URL` is required. When it is missing or invalid, the chart area shows a configuration warning instead of loading a hardcoded fallback URL.

To run the UI against the bundled activity, chart, run, and replay mock data instead of AdofaiIpc, open the development URL with `?mock=1` (for example, `http://localhost:5174/?mock=1`) or start Vite with `VITE_USE_MOCK_ACTIVITY=true`.

The web source uses a one-way layered architecture. Each layer is subdivided by domain where applicable:

```text
shared clients/UI → schemas → models → api → state/mocks → hooks
                  → components → sections → pages → app
```

High-level application services depend on the generated `ReplayApiMod` methods through application-owned ports in `web/src/ports`. The adapter registers the generated app callbacks before discovery; only the adapter and composition layers use the IPC runtime. The SDK validates the TypeSpec contract on both sides. Explicit typed mappings adapt its lowerCamelCase models to the existing application DTOs, where Zod retains feature-specific validation and defaults. File consumers use `ReceivedFile` capabilities through narrow ports, without handling transfer tickets or URLs. Feature messages update TanStack Query caches; calibration editing state stays in its feature reducer. Hooks act as page/component ViewModels: they own application state, derived display values, and commands, while pages and components focus on composition and rendering. The app composition root is the only place that selects the production or mock `AppApi` bundle. Canonical shadcn primitives live in `web/src/shared/ui` and cannot import domain code.

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

## Typed AdofaiIpc API

TUFReplay uses **ADOFAI-IPC 2.1.0**, WebSocket wire protocol **3**, and TUFReplay
health compatibility version **11**. Its source contract, [`ipc/main.tsp`](ipc/main.tsp),
defines 51 typed mod methods and 10 typed app callbacks. Generated calls replace
the recorder's previous named-command/correlated-event protocol. The SDK owns
request correlation, contract validation, app identity, file transport, and errors.

The contract belongs to this mod repository. [`adofai-ipc.config.json`](adofai-ipc.config.json)
generates C# into `TUFReplay/Shared/Ipc/Generated` (compiled with the mod) and the
private TS package `@tuf-replay/ipc` into `web/vendor/tuf-replay-ipc` (pinned with
a relative file dependency). Both outputs are committed. The TypeSpec generator
is pinned by the committed package under `vendor/adofai-ipc-typespec`; no sibling
checkout, absolute developer path, or external symlink is required to build.

The companion installs the published `adofai-ipc@2.1.0` npm package with an exact
version and frozen Bun lockfile. `web/vendor/adofai-ipc/source.json` records its
clean upstream revision, npm tarball integrity, and SHA-256 inventory. That
directory contains provenance and license documentation, not imported SDK source.
Production builds verify the installed package and emit `/adofai-ipc.json`.
CI and Docker check the same package and generated-contract inventory before deployment.

After installing dependencies with `bun install --frozen-lockfile`, use:

```bash
./scripts/run.sh ipc-generate
./scripts/run.sh ipc-check
./scripts/run.sh ipc-test
./scripts/run.sh mod-check
./scripts/run.sh migration-check
./scripts/run.sh web-check
```

`ipc-generate` refreshes both generated packages after a contract edit.
`ipc-check` checks that the generated files are current and that the installed
npm SDK matches its pinned provenance. `ipc-test` connects the bundled real IPC
runtime to the installed TS SDK for bidirectional calls, app-targeted delivery,
file reads, reconnect snapshots, and cancellation without starting ADOFAI.
`mod-check` builds and tests without installing anything in the game; `build`
retains its explicit build-and-install behavior.
After `package`, `migration-check` verifies the pinned, actually published beta.4 ZIP,
executes its old updater against the new local package, and verifies SDK promotion
and next-launch state without changing the installed game Mods folder.

| Feature | Generated mod methods | App callback |
| --- | --- | --- |
| Health and activity | `readHealth`, `listAppSessions`, `readLegacyReplayStatus`, level/session/run/chart readers, `removeRun` | `healthSnapshot`, `activityChanged` |
| Replay and native picker | `startReplay`, `readReplayState`, `chooseLevelFile` | `replayStateChanged` |
| Microphone | device/access/timing changes, recording export/remove/retain | `microphoneDevicesChanged`, `microphoneTimingChanged` |
| Calibration | start/state/result/preview/timing/close methods | `calibrationStateChanged` |
| Camera | `refreshWebcamState`, `changeWebcamSettings`, `readWebcamPreview`, recording export/remove/retain | `webcamStateChanged` |
| Dependencies and bundle preparation | FFmpeg/download actions, `prepareRenderBundle`, `readRenderBundleState`, `cancelRenderBundle` | `ffmpegStateChanged`, `downloadsStateChanged`, `renderBundleStateChanged` |

App callbacks preserve broadcast state changes and responses or notifications
targeted to the requesting app. Initial registration and each new session receive
fresh snapshots. The web adapter captures callbacks before the first subscriber,
retains bundle states by job identity, and clears stale snapshots when the session
becomes unavailable. Reconnection refreshes application caches without replaying
user actions. Native picker disconnection cancels the owning app's pending selection;
mod disable disposes registration and its pending work.

The generated contract uses concrete models, arrays, nullable values, optional
fields, string literals, float64 values, and exact `safeint` counters/byte lengths.
C# optional patches use `IpcOptional<T>`: omission retains a setting, while an
explicit nullable field can clear it. The native level picker selects a file on
the game computer; there is no app-to-mod file upload.

### File consumption

Chart files, saved microphone WAVs, camera MP4s, and BMP preview frames are
`ipc.File` values. The mod provides `IpcFile.FromPath`, `FromStream`, or
`FromWriter`; the companion receives `ReceivedFile`. File bytes never enter
the control-message payload, and business code never interprets transfer tickets
or switches between HTTP and WebSocket itself.

Each received file can be consumed once. Reading or handing it to the browser
must start within 60 seconds and within the session that issued it. After expiry,
cancellation, or disconnection, request a fresh file rather than retrying the old
handle. File responses are consumed immediately and are not kept in snapshot caches.

- Chart reads return `{ levelSessionId, floorCount, file }`. The application
  streams the file through its strict UTF-8 decoder, verifies the byte count,
  cancels obsolete reads, and passes the resulting text to the existing chart
  consumer. Large charts remain separate from the 2 MiB control-message limit.
- Camera preview returns `null` during warmup or `{ width, height, file }`.
  A single sequential reader validates the bounded BMP length and header before
  presenting it. Preview cancellation stops pending reads without stopping recording.
- Recording save actions call `await file.download()` immediately. The SDK hands
  the transfer to the browser's native download manager without buffering the
  complete recording in JavaScript. Resolution means **handoff**, not download
  completion. The browser's download settings determine the destination folder
  and whether it asks for a location.

The microphone timing dialog retains global offset and gain editing, transient
calibration waveforms, and game preview playback. Calibration recordings are not
written into activity history. Every calibration starts from raw uncorrected
capture timing. The saved offset and `-20 dB` to `+30 dB` gain remain applied to
stored microphone playback, with the existing true-peak limiter. Calibration
preview clock callbacks remain limited to 20 Hz and the UI interpolates between
samples. Installer progress callbacks remain limited to ten per second.

Camera discovery stays explicit and uses its existing cache. Capture state,
completed discovery, and saved in-game layout changes publish `webcamStateChanged`.
The companion's crop preview reads the game's shared camera through typed files;
it does not open a browser MediaStream or a second camera device.

## Tech Stack

- **C# / .NET SDK**: mod implementation and build tooling.
- **netstandard2.1**: target framework for Unity compatibility.
- **UnityModManager**: ADOFAI mod loading.
- **AdofaiIpc / TypeSpec**: generated C# and TS contracts, app sessions, shared localhost transport, and streaming file delivery.
- **Harmony**: game method patching for recording hooks.
- **TUFHelperLite**: optional public TUF level ID resolution for downloaded charts.
- **SQLite / Microsoft.Data.Sqlite**: local play record storage.
- **Newtonsoft.Json**: metadata and API JSON serialization.
- **Bun / React / Vite**: companion activity explorer and embedded chart bridge.
- **Bash / .env**: local build and install configuration.

## Special Thanks

- **Teo** — Gave his idea to me and started this project.
- **Potato** - Developed CReplay, a mod that was heavily referenced in this project.
