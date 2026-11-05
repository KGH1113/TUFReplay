# Microphone sync fixes (2026-11-04)

Status: implemented, `mod-check`, `web-check`, `ipc-check` and the macOS helper self-test pass. Not yet verified in game.

## Causes found

1. **Input offset.** ADOFAI computes `songposition_minusi = (dspTime - dspTimeSong - calibration_i) * pitch - addoffset`, so the song audio being mixed at any replay time is one input offset ahead of the timeline. Calibration aligns key sounds to native input times, and replay mixed each sound at that time, so it was heard one input offset late. Playback now moves the mic by the replay's applied input offset (`ReplayPlaybackSnapshot.GameInputOffsetUs`). A key sound is then heard at the song position where its input was judged. Render bundles add the recorded `gameInputOffsetMs` to `latencyUs`, because OrbitRender mixes the song with the same relation.
2. **Single conductor anchor.** The recording was aligned from one conductor update. ADOFAI's `dspTime` advances once per audio buffer (21 ms at 1024/48 kHz, up to about 45 ms), so that one pair was stale by a random amount and each run had a different mic offset. `MicrophoneCaptureTimeline` now averages every anchor of each two-second window.
3. **Unscheduled replay start.** `AudioSource.Play()` started at the next mixed buffer, using the frame computed from a stale song position, and nothing corrected it afterwards. Playback now uses `PlayScheduled` on the DSP clock with the frame for that exact DSP time (`snapshot.TimelineTimeAtDspUs`), and reschedules if it is more than 30 ms away from the timeline.
4. **Time that stood still.** A pause (including ADOFAI's focus-loss pause) and the EnhancedCountdown frozen wait kept being recorded while the timeline stood still, and one linear offset put that audio into the replay. The timeline map starts a new section on a pause or rate change, so that audio is skipped. For a frozen start, audio before the first input (minus 100 ms for the input's own sound) is muted (`AudibleFromUs`). Before this change, the metronome from the wait played during the replay's normal countdown.
5. **Windows first sample.** `Microphone.GetPosition` advances once per capture block, so the start time from one reading could trail by a block. The backend keeps the tightest bound from the first five seconds of readings.
6. **macOS lost buffers.** The helper appended buffers without checking their PTS. A lost buffer moved every later sample earlier. Gaps are now filled with silence and overlaps are dropped (5 ms tolerance).

EnhancedCountdown needed no change for these. Its `TufReplayPlaybackDetector` still disables it during replays.

## Storage

`microphone_recordings.timeline_json` (nullable TEXT) is added in place; schema stays 1. Older recordings keep `capture_start_offset_us`. `CaptureStartOffsetUs` is still written from the first map point for older readers and OrbitRender.

The render bundle rewrites `microphone.wav` when needed (`MicrophoneTimelineTrack`). It cuts paused audio and silences the frozen wait, so one offset stays correct.

## Gain

Gain matches OBS Studio's Gain filter: -30..+30 dB stored in 0.1 dB steps, `powf(10, dB/20)` and a hard clip. Since 2026-11-05 it is set in ADOFAI's audio settings instead of the companion (see Volume below). OrbitRender still applies `alimiter=limit=0.95` to its mix, so rendered audio is limited instead of clipped.

## Renders (2026-11-05)

A render of a checkpoint run (start tile 2296) had the mic 109 ms ahead of the song. Cross-correlating the video's audio with the song and with the mic WAV showed the timeline map was fine; the song was late.

1. **EnhancedCountdown during renders.** EC arms in the editor whenever the start tile is above 0. It only stood down for autoplay and TUFReplay's own playback, so it also armed while OrbitRender rendered. It then rescheduled the song on Unity's DSP clock while OrbitRender drove the conductor, the picture and the mic placement on its render clock. The gap differs per render. EC now falls back to the native countdown while `OrbitRender.RenderApi.IsBusy` (or `RendererController.ControlsTime` on older OrbitRender) is true. This is merged into EC `main` after `fix/frozen-start-audio-sync`, not released yet.
2. **Input offset in renders.** OrbitRender keeps the recorded input offset so judgments match, and ADOFAI draws planets one input offset behind the mixed song. The song, hit sounds and mic were therefore one input offset ahead of the picture. imnyang/OrbitRender#8 delays all rendered audio by that offset. TUFReplay keeps adding the offset to `latencyUs`, because OrbitRender places the mic against the game audio.

The saved mic offset was 0 ms in that run, so capture latency was not compensated.

## Volume (2026-11-05)

In-game replays played the mic much quieter than renders at the same +20 dB gain. ADOFAI sets `AudioListener.volume = 0.5 * (volume / 10)^2` every frame (0.5 at the default 10), and that also scaled TUFReplay's mic `AudioSource`. OrbitRender's captured game audio already includes it, but the mic is mixed in FFmpeg without it, so the render mic was 1/`AudioListener.volume` louder against the game. The render matched the settings: song at -21 dB (calculated -20 dB from volume 10, music volume 4 and the song source's 0.8) and mic at x6.3 (x10 gain, -3 dB for FFmpeg's mono-to-stereo split, about 1 dB of limiter).

The mic source now sets `ignoreListenerVolume`, so ADOFAI's volume settings no longer change it. The gain moved from the companion to a **TUFReplay Microphone** row in ADOFAI's audio settings (5 dB steps, 1 dB with Shift, Off below -30 dB). In-game replays, the calibration preview and renders all use it; Off leaves the mic out of renders as well. The companion's gain controls and the `changeMicrophoneVolume`/`changeCalibrationVolume` IPC methods and `microphoneVolumeDb` fields are removed, so the health protocol is now 16.

## Remaining

- First-tile replays still align their origin on the replay's PlayerControl frame (`AlignFirstTilePlayerControlOrigin`). The recorded and replayed song-position origins differ there by about 0.86 s in tests, so the alignment is needed. It inherits up to one frame plus one DSP buffer of error, and that error moves inputs, mic and camera together relative to the song.
- The camera still anchors each segment on one conductor update (`WebcamCaptureTimeline`).
- Mic clock drift against the output clock is not measured (about 3.6 ms over 3 minutes at 20 ppm).
- EnhancedCountdown resumes the song with `UnPause()` rather than `PlayScheduled`, so the song can start up to one audio buffer after the conductor assumes.

## In-game check

- Log `[Recording/Microphone] Capture aligned ... points=N, audibleFromUs=...`. A frozen start shows a value; a normal countdown shows `start`.
- Log `[Replay/Microphone] Playback scheduled ... inputOffsetUs=...`.
- `Playback stats ... resyncs=` should stay 0 without pauses, and be 1 per recorded pause.
- With a non-zero input offset, a perfect hit's key sound should land on the beat.
- Render: log `[Replay] Delaying render audio by the recorded input offset: N ms.`, and no EnhancedCountdown metronome panel appears during the render.
- Volume: at the same **TUFReplay Microphone** value, the replay mic is as loud against the song as in a render of the same run, and changing ADOFAI's master volume changes only the game. `Player ready ... volume=` and `Playback scheduled ... gain=` show the applied value.
