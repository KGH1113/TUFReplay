# Visual presets: direct implementation and local E2E verification

Verified 2026-09-16. This follow-up was implemented directly without subagent delegation. It supersedes the old 5187 fixed-frame demo.

## Open local results

- Asgore actual player: http://127.0.0.1:5190/tests/fixtures/webgpu/asgore-review.html
- Actual TUF pass with iframe replay: http://127.0.0.1:5176/passes/5
- Registration/submission test UI: http://127.0.0.1:5189/tests/fixtures/visual-e2e.html

The local TUF backend uses port 3002, submission API 5151, test transport bridge 5152, and the existing submission lab 5188. MySQL 3307, Redis 6380, Elasticsearch 9201, submission PostgreSQL 5432 and ingestion Redis 6379 are local test instances. These processes remain running for inspection.

## Corrected behavior

- Registration inspects missing asset references before upload. Users attach required fonts/images, which the production C# import adapters validate and include in immutable bundles. Jipper's missing default font no longer silently becomes a system font. Browser limits reject empty/oversized files before reading them; the importer enforces decoded asset and bundle limits.
- The player resolves separate keyviewer/overlay assets and waits for FontFace/image loading before its first rendered frame. Font registrations are released on disposal and asset resolver replacement.
- DMNote layout computes visible content bounds and note-track padding; counters, active/inactive styles, images, KPS elements, note positions and travel use source settings. Default note speed is 400 px/s; the supplied numpad preset uses 450 px/s and a 300 px track.
- Jipper follows enabled feature settings, default status rows, actual uploaded font, judgement counts/colors, combo tiers, Timing Scale and attempt placement. Unrecorded attempt history is shown as `-`. TBPM preserves decimals; CBPM uses chart intervals rather than input timing jitter. Modern XAccuracy is evaluated during playback and uses recorded final accuracy at the clear boundary.
- Basic text tags are parsed declaratively. No imported JavaScript or arbitrary HTML executes. Source code/CSS implementations were not copied.

## Browser E2E evidence

1. The companion registration UI inspected Jipper defaults, requested `Font/MAPLESTORY_OTF_BOLD.OTF`, accepted the attached font and registered an overlay through the actual C# importer and Rust API.
2. A multiple-tab DMNote export was rejected. The user-approved `numpad` single-tab copy registered with its embedded font through the same importer/API.
3. The actual submission gallery initially had no selection. One keyviewer and one overlay were selected independently.
4. A recorded P16 fixture (`The Limit Does Not Exist`) was uploaded, validated by the local test flow, registered by the actual TUF backend, and became pass 5. The server run is `1ee0248d-1173-4e02-8ad0-b20b8d0a6ec1`.
5. Its format-3 manifest contains both saved selections. Repeating submit returned the same pass and preserved both selection IDs.
6. The TUF frontend loaded protocol-2 web-adofai in its real iframe. Play rendered the chart plus DMNote keys/counter/KPS and Jipper overlay with the uploaded fonts.

Saved overlay: `d913bde7-37dc-428d-9355-9e03814f281c`. Saved keyviewer: `dae8ba0a-2940-4cf8-8bb1-6cb49e3f9174`.

The test UI substitutes the live Unity IPC transport with a local bridge. It invokes production C# import code and real authenticated HTTP services; registration and submission are not mocked. Historical validation fixtures/trusted tester configuration mean this is not proof of live anti-cheat capture or a live in-game login session.

## Requested Asgore record

- Original DB was opened read-only. Run `f689436ba19b4f4090dc704b8b785de2` is cleared with Too Early = 3.
- 11,896 input events and 5,271 hit contexts; recorded end 394.9 seconds.
- DMNote `custom-1782579510617` (`numpad`) and Jipper defaults are loaded from the bundles downloaded from the actual registered submission.
- This page uses the production ReplayPlayerController, CSV parser, visual compiler/evaluator and WebGPU renderer, with chart audio/images from the user's local map.
- Verified continuous playback through 27.8 seconds, changing keys, rain, KPS, combo and chart visuals; seek to the 394.9-second recorded end showed Too Early = 3 and final counters; backward seek restored the initial state. Both actual font faces loaded.
- The full 394.9 seconds were not watched continuously. End-state seeking and automated terminal-tail tests are distinct from that.
- Asgore has U6 metadata and is not eligible for the local auto-submission policy. Its rank was not altered to force submission. The P16 record above verifies the full registration path; Asgore verifies the requested real rendering data separately.

## Checks

- `./scripts/run.sh mod-check`: passed (build/native/Mono, visual import, submission and updater tests).
- `./scripts/run.sh web-check`: 141 tests / 18,815 assertions, TypeScript, Biome and production build passed.
- web-adofai: 904 passed, 1 existing skipped; TypeScript and production build passed. Subsequent Timing Scale/default-speed changes were checked again with focused visual tests and TypeScript.
- New tests cover binary font uploads across encoding chunks, pre-read size rejection, nested basic rich text, chart BPM independent of recorded jitter, and Modern judgement state after backward seek.
- `git diff --check` passed in the replay and editor workspaces.
- The E2E helper's broad typecheck still includes a pre-existing maintenance script (`src/local-tuf/reset-demo-submissions.ts`) that statically imports the sibling backend without its TypeScript aliases. Its backend alias/type errors are not a passed check.

## Reproduction files and limits

`web/vite.visual-e2e.config.ts` launches the production companion components against the bridge. `tools/auto-submission-e2e/src/fixtures/prepare-visuals.ts` prepares private local inputs. `prepare-asgore-review.ts` prepares the local Asgore page from the read-only DB, extracted record and real preset endpoints. These are developer fixtures; private chart/audio/font data stay in ignored local directories and are not committed.

Advanced DMNote CSS and all source-specific effects are still not demonstrated as visually equivalent; the implementation supports selected declarative paint/text properties and approximates remaining effects. Mouse/HID movement missing from historical keyboard-only records cannot be reconstructed. Live in-game IPC upload and 1080p60 performance measurement remain separate verification work.

No deployment, game installation or git commit was performed. Original JSON and SQLite files were not modified.
