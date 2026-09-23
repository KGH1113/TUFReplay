# Visual preset implementation contract

Delivery update (2026-09-23): web-adofai now downloads and validates visual bundles.
The TUF frontend sends record IDs only and no longer enumerates sources or transfers
bundle buffers. The [protocol-3 contract](replay-delivery-contract.md) supersedes
the host-download portions of this historical implementation record.

This document supersedes the options in the earlier investigation with the user's decisions on 2026-09-16. Application implementation is authorized. Upstream source may be inspected for behavior, algorithms, constants and layout, but must not be copied into this project.

## Product invariants

- One optional keyviewer and one optional overlay per submission, independently selected. No combination presets, remembered selection, defaults, preview, placement editing, viewer configuration or speed control.
- Sources: JipperResourcePack (both kinds), DMNote, Impl DMNote and standalone Jipper KeyViewer (keyviewer only), and ImplResourcePack (overlay only). See [additional source formats and rendering boundary](visual-sources-2026-09-17.md). Read only current saved settings. A registration always creates a fresh immutable snapshot with a separately supplied name unique among that owner's active presets (across both kinds).
- Gallery artwork is the first grapheme of the name. Both selections initially empty. Empty means nothing is drawn; no generic keyviewer fallback.
- JS/plugins and sound are silently excluded. Unsupported optional fields are ignored without import warnings. Missing referenced images/fonts fail import. DMNote multi-tab exports import only the saved selected tab (updated 2026-09-17); missing/stale selection fails with an instruction to select a tab and export again. Single-tab exports without selection remain supported. Stored bundles contain only one tab. Non-key visual elements and CSS appearance are in scope.
- Original layout/quirks should be approximated as closely as possible, proportional fit of the complete reference viewport. No quality reduction controls. Target 1920×1080 at 60Hz.
- Submitted selection is fixed across retries, including ambiguous remote registration outcomes. Deleting a preset clears its appearance in existing submissions on the next replay load; it must not serve stale cached data. No public sharing/download UI or replacement.
- Authentication stays in the mod; web uses IPC. No browser access tokens, independent login, or generic local-path reader.
- Replay goes through recorded termination, including post-clear input. Use replay time for key/stat evaluation because existing seek reconstruction uses that clock; no separate viewer-session counter state. Missing historical metrics render `-`.
- Asset provenance must permit use; otherwise use an independently created replacement. No upstream renderer/source copying.

## Wire snapshot (v1)

All API JSON names below are snake_case. Source-specific file contents preserve their original field names.

```ts
type VisualKind = 'keyviewer' | 'overlay';
type VisualSource = 'jipper-resourcepack' | 'dmnote' | 'impl-dmnote' | 'jipper-keyviewer' | 'impl-resourcepack';
type VisualBundle = {
  schema_version: 1;
  kind: VisualKind;
  source: VisualSource;
  source_version: string;
  viewport: { width: number; height: number };
  files: Record<string, unknown>; // logical allowlisted config names, never absolute paths
  assets: Array<{ path: string; media_type: string; data_base64: string }>;
};
type VisualPreset = {
  id: string; name: string; kind: VisualKind; source: VisualSource;
  source_version: string; created_at: string;
};
type VisualSelection = { keyviewer_id: string | null; overlay_id: string | null };
```

Jipper logical keyviewer config is `KeyViewer.json`, overlay config `ResourcePack.json` (adapters map actual saved names). DMNote and Impl DMNote use `preset.json`. Standalone Jipper KeyViewer uses `JipperKeyViewer.json`; ImplResourcePack uses `ImplResourcePack.json`. Assets are identified by safe relative logical paths; remote URLs and traversal are not asset resolution mechanisms. The importer collects referenced bytes, and the replay package resolves only those bytes. JavaScript/audio fields are stripped before storage and never executed.

The stored bundle is source data, not executable renderer code. Independent source adapters in web-adofai compile it once to a common draw model. This keeps C# collection, storage/auth, source compilation, replay evaluation and GPU drawing independently testable. Renderer adapter behavior is versioned by the bundle schema and source version.

## Authenticated API

- `GET /api/v1/visual-presets` → `{ presets: VisualPreset[] }`, current owner only; metadata only.
- `POST /api/v1/visual-presets` body `{ name: string, bundle: VisualBundle }` → `{ preset: VisualPreset }`.
- `DELETE /api/v1/visual-presets/{id}` → `{ deleted: true }`, owner scoped, tombstone snapshot. No update endpoint.
- Existing `POST /api/v1/runs/{id}/submit` gains optional `{ presentation: VisualSelection }`.
- Run DTO includes `presentation: VisualSelection | null`: null means selection is not fixed yet; an object means a fixed selection, including two empty slots. The web gallery opens only for the first selection; retries omit presentation and preserve the server's fixed selection.
- Missing body/presentation: keep existing fixed selection, or fix both null for first submission. Explicit selection validates ownership/kind/active state and compares against frozen selection on retries. Pre-migration requested/submitted records are fixed empty. Serialize selection fixation and existing state transition with DB row lock + transaction.

Error identifiers: `visual_name_required`, `visual_name_taken`, `visual_source_unsupported`, `visual_bundle_invalid`, `visual_asset_missing`, `visual_payload_too_large`, `visual_multiple_tabs`, `visual_preset_not_found`, `visual_selection_conflict`.

Resource limits are safety bounds, not quality adaptation: 64 MiB request/bundle, 32 MiB decoded individual asset, 128 MiB aggregate decoded assets, 4096 assets, 32768 JSON nodes (to be reconciled with real fixture structure). Validate finite viewport dimensions, JSON depth, filenames, media signatures and supported non-executable image/font types. Never fetch arbitrary asset URLs on server or browser.

## IPC (mod-owned auth)

- `visual.presets.list` → list API response.
- `visual.presets.remove { id }` → delete response.
- `visual.sources.get` → installed/supported sources and versions (DMNote web import always listed).
- `visual.presets.import { name, kind, source, presetJson? }`: Jipper Resourcepack, Jipper KeyViewer, and ImplResourcePack automatically read the detected installed mod’s current saved settings. Configuration JSON uploads for these sources are rejected; DMNote and Impl DMNote supply exported JSON. The mod bundles/sanitizes settings and referenced assets and sends the authenticated POST. Missing images/fonts can still be attached through `assetsJson`. Large I/O stays off Unity main thread. Return `{ preset }`.

Source discovery reports installed availability. The web requires it for installed sources and presents no configuration-file picker. Discovery checks the current Mods directory using Info.json IDs, including renamed folders; it does not scan unrelated backup directories. Registration rereads the saved files to capture current settings.
- `submission.run.submit { runId, presentation? }` forwards body.

## Replay delivery

- Existing bare/v2 replay manifest stays compatible. `GET /api/v1/replays/{runId}?format=3` returns v3 with `visuals: { keyviewer: VisualDescriptor|null, overlay: VisualDescriptor|null }` and `Cache-Control: no-store`.
- Descriptor: `{ preset_id, name, source, source_version, url, sha256, bytes }`.
- URL exactly `/api/v1/replays/{runId}/visuals/{keyviewer|overlay}`. Route requires published replay + that run's fixed selection + nondeleted preset; response bundle JSON, `no-store`, byte/hash metadata matches descriptor. A deletion between manifest and bundle fetch may return 404 and is treated as an empty slot.
- FE downloads descriptors with `cache: 'no-store'`, validates identity/path/size/hash and transfers `visuals: { keyviewer: { descriptor, data: ArrayBuffer }|null, overlay: ... }` alongside existing level/replay buffers.
- iframe protocol v2 accepts v3 manifest and visuals, with v1 compatibility for existing consumers. New host sends only play/pause/seek/restart/dispose. In v2, reject/ignore settings changes and use fixed defaults (pitch 100, generic hitErrorMeterVisible false). No generic keyviewer or generic HUD fallback in empty v3 slots. Ready capabilities for v2 are `{ visuals: true, fixedPresentation: true }`.
- Validation/score evidence digest remains independent of presentation. TUF backend registration API remains unchanged.

## File ownership for parallel implementation

Root: contract, TUF FE transport/UI, integration and final verification.
Server worker: `server/` models/migrations/services/controllers/tests only.
Mod worker: C# visual collection/IPC and submission HTTP body changes, associated tests only.
Companion web worker: `web/` import/library/gallery/API/mocks/i18n/tests only.
Renderer worker: staged adofai-web-editor source adapters/evaluation/rendering/protocol/player/tests only.

Sibling repository edits are prepared in `/private/tmp/tuf-visuals-20260916/{adofai-web-editor,t21c-web-frontend}` and will be applied to the user's original checkouts after inspection. Existing user changes are preserved.


## DMNote registration placement and size (2026-09-17)

Registration stores `tufReplayPlacement: { x, y, scale }` inside `files["preset.json"]`. Coordinates are the top-left anchor in the 1920×1080 replay viewport. Scale is finite, from 0.1 to 4; omitted scale in existing presets means 1. The registration editor shows a percent slider/input and clamps integer coordinates against scaled bounds. A viewer larger than the screen stays anchored at zero on the overflowing axis and is clipped by the viewport.

The C# importer and submission server validate the scale and preserve it in the immutable visual bundle. TUF backend registration continues to store the run association; TUF frontend downloads and transfers the integrity-checked bundle bytes unchanged. The web ADOFAI compiler/evaluator carries placement metadata into the renderer, which scales the complete keyviewer about the stored anchor. Key labels, CSS-derived styling, counters, images and rain share this transform; overlay geometry and playback timing remain unchanged.

Changing size requires registering a new preset. Existing submitted presets are not rewritten.

Verification for size: companion web tests (147), C# mod-check and server library tests (36) passed. A cross-repository check sent 0.5/1/2 scales through `prepareDmnoteImport`, the production C# importer stdin bridge, TUF frontend `loadReplayVisuals` hash verification, transferable ArrayBuffers and web ADOFAI's delivery parser/compiler. This uses a synthetic fetch response, not a newly registered live TUF pass. The browser WebGPU fixture `tests/fixtures/webgpu/dmnote-scale.html` rendered all three scales with measured key bounds matching expectations, visible rain, counter state 1 and identical overlay pixel `[4,166,218,255]`.
