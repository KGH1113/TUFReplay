# Shared camera preview and Windows recording recovery

## Evidence

The Windows tester log from 2026-10-06 shows successful DirectShow opening and first frames on all four arm operations. The capture continues publishing frames at the failing finalization (`sequence=18604`, `hasFrame=true`). At 12:53:52 UTC, `FfmpegCameraRecording.FinishAsync` throws its generic recording-finalization error. The old error handler clears `_armed` despite the healthy capture; two subsequent starts are skipped while a new camera/encoder warms up. The old diagnostic does not distinguish encoder exit failure from zero written frames, so the exact encoder trigger cannot be established from this log alone.

The same log includes UMM `Logger.WriteBuffers` collection-modified exceptions. Camera diagnostics previously wrote to that logger from workers. Diagnostic serialization remains off the game thread; publication and camera exception logging now use the existing main-thread dispatcher.

## Ownership and transport

TUFReplay owns the single physical capture session. The companion no longer calls `getUserMedia`, enumerates browser cameras, matches device labels, or opens another device. The obsolete browser capture adapter, session, mock, and matching tests were removed.

After explicit reveal, the API issues `webcam.preview.read` with the currently selected native `deviceId`. A changed device or disabled camera rejects stale requests. `webcam.preview.frame` returns either `{ready:false}` while warming or a single-use HTTP download ticket with width/height metadata. Bytes never enter a WebSocket JSON envelope. IPC protocol is now 10; ADOFAI-IPC wire protocol remains 3 and its vendored SDK remains unpublished.

The mod copies a complete shared RGBA frame independently of the game's consuming sequence, converts it to a bottom-up BGR BMP on an IPC worker, and caps output at 640 × 480. Padding and actual aspect ratio are preserved. Source storage is reused, and a shared cache bounds conversion to eight frames per second. Every ticket owns an immutable bitmap snapshot. Requests renew a one-second preview-demand lease; hiding the in-game overlay cannot overwrite browser demand. The lease expires without needing a close event. Resuming after idle allows the capture to refresh; three seconds without source progress rejects a frozen camera.

The application-owned `CameraPreviewDownloads` port separates HTTP transport from the webcam API. The production composition root injects the local download adapter. It validates local ticket addresses, dimensions, exact bounded byte count, and BMP headers, and cancels incomplete reads. `startGameCameraSession` accepts an injected reader, permits one in-flight request, owns abort/timeout/polling, and rejects late frames after teardown. The React hook only manages visibility, reveal state, image URLs, and presentation. Close, hide, camera changes, camera off, connection loss, page exit, tab hiding, and unmount release requests and URLs. No browser permission prompt is needed.

## Recording and transforms

An encoder that writes zero frames without a writer error finishes as an empty recording and removes its temporary file. A genuine encoder failure logs exit code, written frame count, writer error, and the last 16 stderr lines. Finalizing a failed recording retains a healthy camera rather than reopening it. Starting the next recording successfully clears the previous recording error; capture-start failures still invalidate readiness.

Horizontal and vertical flip are independent persisted display settings. They do not rearm capture or change source recording pixels. Crop coordinates refer to the untransformed source; web gestures invert both axes back into those coordinates. In-game live/replay and setup previews use the corresponding UV transform. Exported bundles include `flipVertical`; the companion Renderer patch applies `vflip` after source crop alongside `hflip` and before viewport clipping.

The two shadcn icon buttons sit eight pixels below the image and are centered on its pixel plane. Hugeicons horizontal/vertical flip icons retain translated accessible names, pressed state, visible selection styling, and shadcn tooltips. No CSS transition is applied to crop tracking or image reflection.

## Verification

- `./scripts/run.sh web-check`: 207 tests, TypeScript, Biome and production build pass. Obsolete browser-device tests were replaced with shared-preview lifecycle, ticket, byte-count, bitmap-header, and vertical-coordinate cases.
- `TUFREPLAY_CAMERA_TEST_FFMPEG=/opt/homebrew/bin/ffmpeg ./scripts/run.sh build`: complete C# and updater suites, macOS helper/native input checks, and Unity/Mono assembly checks pass; installed to the user's Mods/TUFReplay folder.
- Shared preview regression: independent game/browser readers, exact bitmap padding/color/orientation, demand expiry, hidden-game-preview demand, encoder-failure recovery, and real FFmpeg zero-frame finalization.
- Renderer `./scripts/run.sh build`: actual FFmpeg composition verifies green/red halves exchange vertical positions; existing media, overlay, engine, bridge and installation suites pass. Installed to Mods/TUFReplay-Renderer.
- In-app browser at `http://127.0.0.1:5176/?mock=1`: explicit reveal, 640 × 360 BMP decoding, crop-keyboard save, both flip buttons and `matrix(-1,0,0,-1,0,0)` confirmed. Final icon group center matches image center exactly, with an 8 px gap. Screenshot saved to `/private/tmp/tufreplay-camera-icons-20261006.png`.

Actual Windows DirectShow hardware and gameplay remain for tester verification. The synthetic tests establish finalization/recovery and composition behavior, not the tester's unknown encoder trigger. Fully restart the game after replacing DLLs, reload the dev website, and use the new Renderer build for vertical flip in exports.
