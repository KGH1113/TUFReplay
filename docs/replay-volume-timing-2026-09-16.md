# Replay volume and timing verification — 2026-09-16

## Implemented

- TUF FE exposes independent music and hit-sound sliders, including mute at 0%. `host.command/setVolume` validates both integer levels in 0–100; the iframe changes Web Audio gains without pausing or restarting. Protocol v2 still fixes pitch and visual settings. Initial volume levels now survive v2 initialization.
- The replay player distinguishes viewer elapsed time from recorded time. Viewer zero maps to the conductor start (`-songOffset` in chart time); the recording's PlayerControl origin, inputs, hit judgments, and terminal timestamps remain unchanged. Both the iframe and Asgore test player use the same elapsed duration and mapping.
- Initial countdown numbers and Hat ticks follow base BPM and countdownTicks. A separate countdown delays song scheduling; a countdown baked into the song starts audio at zero. Seeking and restarting use the expanded timeline. Seeking to the end while playing now stops instead of starting over.
- The FE displays elapsed / total time alongside percentage.
- Local E2E fixture extraction now retains inputs through terminal time, while scored hits and submission key count stop at won time. The synthetic Won lifecycle event uses wonTimeUs rather than recording duration. The extractor reads the current replay_artifacts table.
- Resize events arriving before GPU/camera initialization are ignored until initialization completes.
- When an initialized iframe sends ready again after a reload, the host establishes a fresh session and retransfers the replay, retaining current volume levels instead of leaving stale controls over a blank canvas.

## Native research

Inspected the installed Assembly-CSharp.dll with ilspycmd 10.0.1, specifically scrConductor.StartMusicCo, GetCountdownTime, the conductor clock update, scrController.Countdown_Update, and scrCountdown.Update. Independent TypeScript implementation uses their timing rules; no C# source was copied.

The native countdown displays countdownTicks - 1 numbers, then Go on the last tick. Separate countdown audio scheduling adds countdownTicks × 60/BPM before song playback. Historical metadata does not retain fast-takeoff / skip-intro preferences; replay reconstructs the normal full level-start countdown, not unrecorded per-attempt intro-skipping preferences.

## Local verification

- web-adofai: 49 relevant tests passed (replay, overlay, audio), TypeScript passed, production Vite build passed. Modified production files passed Biome.
- TUF FE: 11 replay tests passed, modified JSX/hook ESLint passed, production Vite build passed. Existing build warnings concern chunk size, file-type eval, and missing Sentry upload token.
- E2E extraction: 2 terminal-window tests passed.
- Browser: music and hit-sound levels changed independently during playback, including mute and keyboard changes; the transport continued playing.
- A new local submission was accepted: pass 6, run `5b59432f-2e77-41d3-bad9-0b6aa23d4744`. Public v3 metadata preserves wonTimeUs 113733580 and terminalTimeUs 128317637, with the existing DMNote/Jipper snapshots. FE total duration is 129.060390 seconds including countdown; playback from 99% reaches 100% and stops.
- Original Asgore fixture: viewer end 397510779µs maps exactly to recorded terminal 394867211µs, and playback automatically pauses there. Countdown 3 is visible at viewer time approximately 1.2 seconds.

## Fixture limitation

Pass 5 was previously sealed using a test fixture truncated at wonTimeUs, so it remains shorter. The existing local P16 fixture retained the original terminal timestamp in its metadata, but its original database run is no longer present in available game databases. Pass 6 restores this known terminal duration; its already-trimmed CSV does not recover any deleted post-clear key events. This limitation is labelled in the fixture supplements. The original Asgore viewer still uses all 11896 inputs through terminal. Newly extracted fixtures preserve complete terminal input tails.

## Review

- TUF: http://127.0.0.1:5176/passes/6
- Asgore: http://127.0.0.1:5190/tests/fixtures/webgpu/asgore-review.html
- Screenshots: `/private/tmp/tuf-visuals-review/tuf-terminal-playback.png`, `/private/tmp/tuf-visuals-review/asgore-countdown.png`.
# Native countdown font follow-up

AssetRipper headless on port 45717 loaded the installed game's complete Data
directory. `RDConstants.latinFont` and `koreanFont` both resolve to `godoMaum`
(sharedassets0.assets, path ID 9), matching `scrCountdown` → `SetLocalizedFont`.
The actual 1,051,120-byte Regular font was exported to the editor's
`public/assets/fonts/replay-countdown/godo-maum.ttf` with its SHA-256 provenance
and the publisher's complete usage terms (`LICENSE.png`). No C# source was copied.

The iframe and Asgore fixture await the same font loader before playback becomes
available. The shared overlay now uses weight 400 with synthetic bold disabled.
Browser verification showed the native handwritten numeral 3 at Asgore 1.2 s and
the same font family on the TUF pass 6 `Get ready` overlay. Relevant 49 tests,
TypeScript checking, production build and diff whitespace checks passed.

Screenshots:
- `/private/tmp/tuf-visuals-review/asgore-countdown-native-font.png`
- `/private/tmp/tuf-visuals-review/tuf-countdown-native-font.png`
