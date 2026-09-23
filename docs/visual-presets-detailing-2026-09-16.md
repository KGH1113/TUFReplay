# Visual detailing verification — 2026-09-16

This follow-up implements the requested DMNote/Jipper detailing directly. Upstream files were read to understand behavior; implementation code was independently written.

## Corrections

- DMNote global `keyCounterEnabled` defaults to false, independently of each key's counter settings. The supplied export omits the global app preference, although its individual counter configurations are enabled. Registration exposes the missing preference and preserves it in the immutable snapshot. Stat counters such as KPS remain visible.
- DMNote display names (Tab, PgDn, LShift, arrows, etc.) are separate from input identities. Explicit `displayText` still takes precedence. Text wraps at spaces and clips rather than squeezing long words. Uploaded fonts, CSS font sizes/weights, and source counter spacing are retained.
- Merging nested glow settings no longer discards rain color, radius, or outline settings. Canvas textures use sRGB. Rain body, outline and glow alpha are separate; outlines support all/vertical/horizontal sides, and track fades use a paint mask. Scratch canvas memory is reused instead of allocating a canvas for every note/frame.
- KPS graph paint no longer inherits the element's transparent border color. Its line is independently colored and clipped to the rounded panel.
- Jipper default progress bar has the 642×18 outer black shape, 638×14 white interior and pastel fill. Default text uses normal font weight and a small shadow. Modern judgement counts retain 11 columns, individual near-perfect buckets, source colors and centered perfect count.
- Asgore review uses the shared production TUF hit-error-meter component/evaluator.

## Actual registration and rendering

Browser: `http://127.0.0.1:5189/tests/fixtures/visual-e2e.html`.

The original full JSON was preserved; the user-selected numpad tab was imported from the private single-tab fixture. `/Users/kgh/games/adofai/adofai-configs/dmnote/custom2.css` was attached separately. The user interface registered `DMNote numpad CSS 위치 검증` through the actual C# importer and Rust API.

- New preset: `bdd70932-b505-4565-a5f4-7e8dca5ce2e7`.
- Saved placement: x=60, y=420 in the 1920×1080 reference frame.
- Saved global key counters: false.
- Stored CSS content matches the attached file exactly.
- Stored 1,898,531-byte snapshot SHA-256 verified before copying it into the ignored Asgore fixture.
- Existing submissions and their fixed preset selections were not changed. `refresh-asgore-visual.ts` reads the local database snapshot for this developer-only review page.

Asgore: `http://127.0.0.1:5190/tests/fixtures/webgpu/asgore-review.html`. The player uses the real clear with three Too Early judgements, the new DMNote snapshot and the previously registered Jipper default snapshot. Both uploaded fonts load. Screenshots are in `/private/tmp/tuf-visuals-review/`.

Rain regression page: `http://127.0.0.1:5190/tests/fixtures/webgpu/rain-detail.html`. Six browser canvas-pixel assertions pass: outline independent of body alpha, 50% outline alpha, round corner, vertical-only border, horizontal-only border, and both track fades.

The updated Asgore player continuously advanced from 30.0 to 319.9 seconds. Seeking to the recorded end reached 394.9 seconds; rewinding returned to 0.0, and the final review tab was left at 30.0 seconds. Key counters stayed hidden while the independent KPS graph/stat continued updating.

Validation: `./scripts/run.sh mod-check` passed, including new placement/global-counter preservation and invalid-coordinate rejection. `./scripts/run.sh web-check` passed (143 tests, TypeScript, Biome, build). The editor full suite passed 905 tests with one existing skip; two additional text/judgement regression tests also passed. Editor TypeScript and production build passed. Both repository diffs pass whitespace checks.

## Follow-up: production TUF iframe hit-error meter

The initial Asgore fixture explicitly enabled its meter, while the production replay page gated both legacy HUD elements behind protocol v1 and protocol v2 fixed settings disabled the meter. Corrected both paths: the fixed presentation enables the meter and only the legacy keyviewer remains v1-only. TUF FE defaults agree with the player, and the Asgore fixture now uses the actual player settings rather than overriding visibility.

Verified the real `http://127.0.0.1:5176/passes/5` replay iframe: meter visible and updating during playback. Added two server-rendered overlay regression tests; these and nine TUF FE replay contract/delivery tests pass. Editor TypeScript passes.

## Compatibility scope

This is verified against the supplied numpad preset and CSS. It does not establish pixel identity for every DMNote preset or arbitrary CSS rule. Declarative CSS remains restricted to supported paint/text properties; JavaScript and sound remain excluded. The earlier tool-workspace typecheck limitation remains as documented in the preceding E2E report.
