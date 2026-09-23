# Additional visual sources — 2026-09-17

Current implementation and verification are recorded in [the 2026-09-21 completion report](visual-source-handoff-2026-09-21.md). The historical verification entries below describe earlier runs.

The source registry, companion registration UI, C# importers, server validation and database constraints now accept:

| Wire source | Display name | Kind | Input |
| --- | --- | --- | --- |
| `jipper-resourcepack` | Jipper Resourcepack | keyviewer, overlay | Existing automatic saved-settings import |
| `jipper-keyviewer` | Jipper KeyViewer | keyviewer | Installed mod `config/settings.json` and selected `config/profiles/*.json` |
| `impl-dmnote` | Impl DMNote | keyviewer | Exported preset JSON, selected tab only |
| `impl-resourcepack` | ImplResourcePack | overlay | Installed metadata, optional UMM `Settings.xml`, fixed overlay layout |

Jipper Resourcepack uses `jipper-resourcepack`; standalone Jipper KeyViewer uses `jipper-keyviewer`. The pre-release `jipper` identifier has been removed without an alias. New installed sources are discovered by `Info.json` identity, including renamed/version-suffixed UMM folders. Discovery and import happen on demand, with no per-frame work or new game patches.

Jipper Resourcepack's default MapleStory font and ImplResourcePack's fixed overlay font use the portable OTF embedded in TUFReplay. DMNote's default Pretendard Variable and Impl DMNote's default SUIT-Regular are bundled with their OFL licenses. An unavailable selected custom font still appears in inspection as a required upload. Standalone Jipper KeyViewer also collects its installed CJK fallback font. ImplResourcePack automatically includes the game's original CJK source font as a lossless embedded WOFF; default registration requires no attachments or other installed mods. Individual asset uploads allow 48 MiB, including the standalone mod's approximately 36 MiB CJK font.

## Format contracts

- **Impl DMNote 0.1.0:** shares the DMNote `preset.json` representation, selected-tab projection, embedded image/font extraction, CSS, counter option and placement/scale controls. Its source identity and application baseline stay distinct. JavaScript/plugins and sound remain excluded.
- **Jipper KeyViewer 1.7.2:** emits `JipperKeyViewer.json` containing `{ Version: 6, Data: <selected profile>, Resources: <portable sprites> }`. Installed sprites, including user replacements, are collected when present; missing default sprites use embedded originals. Preserves native profile fields, fixed-layout settings, FreeMake nodes and layer groups. Configs requiring the source mod's older coordinate migrations are rejected; save them with the current mod first. Only the selected profile is read. Relative image paths use `CustomImages`; contained absolute paths become portable relative paths. External images require explicit attachment. Named bundled/custom fonts are resolved from installed files. A font recorded only by runtime index is resolved from the running mod's actual font list; bundled/custom files are collected, common system families are retained, and an unknown or unavailable font still requires attachment. This importer currently discovers UMM installs with metadata; metadata-free MelonLoader installs are not advertised.
- **ImplResourcePack 0.1.0:** emits `ImplResourcePack.json` with `layoutVersion: 1`, a 1920×1080 reference viewport, Status/BPM/Combo/Judgement panel geometry, theme colors, `HidePerfectJudgmentText` and `RecordMode`. The original overlay is fixed in code, not a user-authored JSON layout. Its default MapleStory OTF and original game CJK fallback WOFF are embedded automatically. Missing XML uses source defaults; XML DTDs/external entities are prohibited. Gameplay input/trackpad options are excluded.

The implementation was checked against the local ImplResourcePack and Impl DMNote sources and [the standalone JipperKeyViewer repository](https://github.com/adofaiex/JipperKeyViewer). The standalone player's fixed coordinate tables are derived from the version-6 layout definitions; rendering uses the replay player's own nodes, slots and evaluators.

## Persistence and rendering boundary

Migration `m20260917_000001_extend_visual_sources` extends the source constraint and enforces source-specific kinds. It converts legacy `jipper` rows, including the bundled source field and SHA/byte metadata, while preserving preset IDs and frozen selections. The designated local E2E row was migrated without deleting its two submission references. Rollback refuses while new-source rows exist instead of deleting user presets.

This repository collects and serves snapshots; replay drawing lives in the separate `adofai-web-editor` project. As of 2026-09-21, both that player and `t21c-web-frontend` recognize all five current source IDs and enforce the same supported kinds. Impl DMNote shares DMNote's selected-tab, CSS, asset and placement pipeline. Standalone Jipper KeyViewer has its own version-6 adapter for fixed layouts, full keyboard, foot keys and FreeMake nodes, including hidden groups and idle/pressed images. ImplResourcePack has a layout-version-1 adapter for Status/BPM/Combo/Judgement and respects RecordMode. The old `jipper` identifier is not an alias.

The completion pass adds source PNG slice/tile rendering, progress-prefab sprites, TMP metrics, Impl song-title delivery, DMNote built-in fonts and pose bounds/easing, portable CSS images, and real ICO/AVIF fixtures. Registration/API byte preservation and native Canvas pixel checks cover all six source/kind combinations. This is source-derived rendering with automated pixel oracles; manual comparison against captured game frames remains with the user. See [the completion report](visual-source-handoff-2026-09-21.md) for the exact font attachment requirements and verification boundary.

Run `./scripts/run.sh visual-check` with the sibling repositories and dependencies present. It compares the mod's source/kind registry with the companion schema, Rust source identifiers, host manifest schema and player bundle schema, so a newly added source cannot silently disappear at a consumer boundary. Player regression tests cover each new compiler and all supported source/kind pairs; the TUF host tests also reject wrong-slot sources.

## Verification

### Consumer integration verified 2026-09-21

- `http://127.0.0.1:5176/passes/8`: loaded the actual recorded replay through the live local API and embedded player, displayed its keyviewer and overlay, and played through `0:12 / 0:12 · 100%`. Browser error log remained empty. Use `127.0.0.1`, matching the live harness's CDN proxy origin.
- Replay player: 920 tests passed, 1 existing sample-dependent test skipped; TypeScript check passed. All 11 touched player source/test files passed Biome.
- TUF host: 9 replay delivery/visual tests passed, including all five sources and wrong-kind rejection; ESLint passed on the two changed files.
- `./scripts/run.sh visual-check`: cross-repository source/kind contract passed (21 assertions).
- `./scripts/run.sh check`: syntax checks passed for 31 shell scripts; ShellCheck is not installed.
- `git diff --check` passed in all three repositories.
- Full player-repository Biome remains blocked by 10 errors and 8 warnings outside this change: existing Asgore fixtures, replay-viewer data files, the TUF parser and its tests. Those unrelated files were preserved.

These checks establish source coverage and working replay delivery. The native-effect limitations above remain explicit; this is not a claim of pixel-perfect parity for every source option. No server data, saved presets, environment files or deployment were changed.

### Original importer verification

C# tests cover independent mod identities, renamed folders, selected Jipper profiles, imported assets, external-image attachments, source-kind rejection, Impl DMNote tab projection and XML/default handling. Web contract tests cover the three new IDs and correct exported-JSON forwarding. Server unit tests cover distinct identity, required logical files, kind restrictions and DMNote-compatible CSS/tab validation.

Verified commands: `./scripts/run.sh mod-check` (build, Unity/Mono compatibility and all C# suites), `./scripts/run.sh web-check` (149 tests, typecheck, Biome and production build), `./scripts/run.sh server-check` (39 library tests and all-target compilation), and `./scripts/run.sh server-check --integration visual_presets` (5 API tests with a newly created isolated PostgreSQL database, removed after verification). Game installation and deployment were not performed.
