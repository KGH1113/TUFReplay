# Replay rendering integration

TUFReplay keeps recording and storage separate from video rendering. It has no reference to
OrbitRender or TUFReplay-Renderer source or assemblies. The independent renderer consumes
schema version 1 files through its own `tuf-replay-renderer` AdofaiIpc namespace.

The web run card's Render button checks `health.get`, exports a bundle through the
`tuf-replay` namespace, starts `render.start`, and polls `render.status.get`. Resolution
presets cover 720p, 1080p, 1440p, and 2160p; output frame rates are 30, 60, and 120 fps.
Camera, microphone, and configured ImplDmNote overlay inclusion are optional. The dialog
shows the renderer's compatibility warnings, cancellation, progress, output path, and a
one-use local download from `render.download`.

## Export lifecycle

`replay.render-bundle.export` takes `{runId, levelPath?, includeWebcam?, includeMicrophone?}`.
Both inclusion flags default to true. It returns a quick job status with `jobId`, `runId`,
`state`, `progress`, `manifestPath`, `errorCode`, and `errorMessage`.
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
`judgmentSystem`, `judgmentDifficulty`, `startTile`, `wonTimeUs`, and `terminalTimeUs`.
All event times are integer microseconds in the recorded replay timeline. The renderer owns
the mapping from that timeline into video time, including countdown and post-clear input.

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

The neutral CSV contract, input normalization, invalid data rejection, cancellation checks,
namespace routing, output option validation, and download URL validation have automated
tests. Unity/Mono build checks do not prove an installed game's frame timing or each overlay
mod's runtime compatibility. Pixel capture and replay input support are distinct capabilities;
the separate renderer reports unsupported or unverified mod behavior as warnings.
Offline Unity frame capture (`Time.captureFramerate > 0`) suppresses new TUFReplay recordings.
