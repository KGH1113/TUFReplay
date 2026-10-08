# Replay rendering integration

TUFReplay keeps recording and storage separate from video rendering. It has no reference to
OrbitRender or TUFReplay-Renderer source or assemblies. The independent renderer consumes
schema version 1 files through its own `tuf-replay-renderer` AdofaiIpc namespace.

The web run card's menu contains the Render action. The dialog checks `health.get` and
`settings.get`, exports a bundle through the `tuf-replay` namespace, starts `render.start`,
and polls `render.status.get`. The renderer embeds the OrbitRender engine, so a separate
OrbitRender mod is not required. Resolution, simulation/video frame rates, codec, encoder,
quality/speed, bitrate or supported software CRF, bit depth, ProRes profile, pixel format,
game audio, end delay, preview, and display flags are editable. Camera, microphone, and
configured ImplDmNote overlay inclusion are optional.

The video is saved in the user's selected local folder on the computer running ADOFAI.
`output-directory.choose` returns a selection ID immediately; the web polls
`output-directory.selection.get` while the native folder dialog runs outside the game
thread. Closing the render dialog cancels a pending selection with
`output-directory.selection.cancel`. The path can also be typed directly. The completed
job exposes `localOutputPath` and `canOpenOutput`; `output-directory.open` takes the job ID
and opens its verified save folder. Completion appears in the web dialog and does not
start a browser download. `settings.update` optionally saves the chosen render defaults.

## Export lifecycle

`replay.render-bundle.export` takes `{runId, levelPath?, includeWebcam?, includeMicrophone?}`.
Both inclusion flags default to true. It returns a quick job status with `jobId`, `runId`,
`state`, `progress`, `manifestPath`, `errorCode`, `errorMessage`, and optional
`errorDetails: {field, line, file}`.
`replay.render-bundle.status.get` and `replay.render-bundle.cancel` take `{jobId}`.
States are `preparing`, `completed`, `failed`, or `cancelled`.

Settings are snapshotted on the game thread. Database access, CSV normalization, hashing,
and streamed media copies run on a worker. Level decode and gameplay hash validation use
the existing game-thread validator. A file SHA-256 checked before and after validation is
included so the consumer can reject a level that changes after export. Level assets remain
at their original paths; a bundle is local to this installation, not a portable level archive.

The exporter accepts current replay-engine recordings with explicit origin, terminal,
pitch, calibration, and judgment data. It rejects unsupported native keys, missing accepted
hit judgments, nonfinite values, events beyond the terminal time, and out-of-order timelines.
It never guesses a missing calibration or drops an unidentified key. Legacy records may
remain playable by other paths while being unavailable to this export contract.

Bundles are written below the mod's `RenderBundles/<jobId>` directory. Only one preparation
job runs at once. A webcam storage lease pins the original MP4 until the owned copy finishes;
microphone WAV data is streamed from its repository. Completed owned copies survive normal
camera or microphone retention. Cancellation and failure remove partial bundles. Older
bundles and finished in-memory job records expire after 24 hours when another export starts.
Mod shutdown cancels unfinished work. Export does not delete or change the original recordings.

## Files

`manifest.json` contains `schemaVersion: 1`, `recordingId`, `level`, `replay`, `inputsFile`,
`hitsFile`, and `media`. The level object includes its absolute `path`, `fileSha256`,
`gameplayHash`, and `gameplayHashVersion`. Replay metadata includes
`gameplayStartSongPosition`, `effectivePitch`, `gameInputOffsetMs`, `noFailMode`,
`judgmentSystem`, `judgmentDifficulty`, optional `hitMarginLimit`, `startTile`, `result`,
`wonTimeUs`, and `terminalTimeUs`. New recordings capture ADOFAI's gameplay hit limit
(`None`, `PerfectsOnly`, or `PurePerfectOnly`) when gameplay starts. Older recordings omit
the setting; consumers must preserve their legacy behavior when it is absent.
Renderer versions that do not read `hitMarginLimit` will continue to use their existing
judgment behavior until updated.
Version 1 exports runs from tile 0 and recorded mid-level/checkpoint starts. The renderer
restores the native checkpoint state and music position. Negative start tiles are invalid;
the renderer also checks that the tile exists in the loaded level. `result` preserves
`cleared`, `failed` or `aborted` so failed runs without an accepted final hit still show death.
All event times are signed integer microseconds in the recorded replay timeline. Inputs
during countdown may be negative; export preserves those times and their stable sequence
instead of clamping or rejecting them. Negative time alone does not imply missing timing
or judgment data. The renderer maps countdown events before gameplay begins, establishes
the definitive gameplay anchor at player control, and keeps the post-clear input tail.
The web dialog's **Wait after clear or death** setting adds 0–30 output seconds after
terminal. On failure it waits for the native death animation to finish first, including
the explosion with a zero delay. This is independent of the recording's pitch.

Renderer job status may include `waitingForGameFocus` (older servers omit it, treated as
false). While automatic ImplDmNote placement cannot read the game window, the web shows
the reason and asks the user to restore and click ADOFAI. This preparation wait remains
cancellable and expires after 30 seconds. Missing focus and bounds still unreadable after
focus have separate timeout errors; manual placement and other capture errors are unchanged.

`inputs.csv` has the exact header:

```csv
timeUs,key,down,sequence
```

Keys are symbolic Unity `KeyCode` names resolved from the recording's native platform,
including aliases such as `Return`, `LeftArrow`, `BackQuote`, `Period`, and `KeypadEnter`.
`down` is 0 or 1. A monotonically increasing sequence preserves press/release ordering,
including short pulses with the same timestamp. Conversion never emits OS keyboard events.

`hits.csv` has the exact header:

```csv
timeUs,floorId,angle,overloadCounter,noFailHit,isAuto,nextFloorAuto,cachedAngle,targetExitAngle,midspinInfiniteMargin,rdcAuto,freeRoamSection,margin
```

Hit geometry, overload state, flags, free-roam section, and resolved `HitMargin` enum name
are preserved. Equal-time hits retain their stored order. Numeric CSV data uses invariant culture.

## Media coordinates and timing

When present, `media.webcam` has an owned relative `path`, source `width` and `height`,
`durationUs`, `captureStartOffsetUs`, `gameplayRate`, piecewise `timeline` segments,
`offsetMs`, `mirror`, `crop`, and `layout`. Segments contain `timelineTimeUs`, `videoTimeUs`,
and `gameplayRate`; they preserve the original pause/speed/clear mapping.

Crop uses normalized source-image edges `{left, top, right, bottom}` with top-left origin.
Mirroring follows crop. Layout uses `{left, top, width, height}` normalized against the
game's screen dimensions at export. Positions outside 0..1 are valid because users can
place the camera partly off-screen. The snapshot preserves the displayed crop aspect ratio.

When present, `media.microphone` contains the owned relative `path`, `sampleRate`, `channels`,
`frameCount`, `captureStartOffsetUs`, `latencyUs`, and `volume`. Positive `latencyUs` moves
the microphone earlier: the corrected start is `captureStartOffsetUs - latencyUs`.
`volume` is linear amplitude gain converted from the saved -20..+30 dB setting, so it may
exceed 1 (up to approximately 31.622). The consumer must apply its limiter when amplifying.
Missing recordings are omitted even when the inclusion checkbox is enabled.

## Validation boundary

Missing metadata fields, malformed CSV values, unsupported keys or judgments, out-of-order
events, and events beyond terminal time have distinct error codes. The web shows localized
recovery guidance and the affected field or row when available. CSV export validation counts
the neutral header as line one; errors encountered while reading native replay payloads
refer to their original payload row.

The neutral CSV contract, signed countdown events, input normalization, precise invalid
data rejection, cancellation checks, namespace routing, output option validation, and
retained download URL validation have automated
tests. Unity/Mono build checks do not prove an installed game's frame timing or each overlay
mod's runtime compatibility. Pixel capture and replay input support are distinct capabilities;
the separate renderer reports unsupported or unverified mod behavior as warnings.
Offline Unity frame capture (`Time.captureFramerate > 0`) suppresses new TUFReplay recordings.
