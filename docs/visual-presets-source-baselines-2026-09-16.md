# Visual preset source baselines

Verified on 2026-09-16. Upstream source is used to understand data formats and observable behavior; application source and styles must be independently implemented, not copied. Images/fonts need separately established usage conditions; otherwise use replacements.

| Source | Target | Commit |
| --- | --- | --- |
| [JipperResourcePack](https://github.com/Jongye0l/JipperResourcePack/releases/tag/v1.5.2.0) | stable 1.5.2.0 | `0ba3ec08cb650c3d848b544cc6d0bde943730b5f` |
| [DMNote](https://github.com/DmNote-App/DmNote/releases/tag/2.0.2) | stable 2.0.2 | `4b4d6c2226efd220844dd7516d06618b9383c64c` |

## Saved source locations

- Jipper uses `<mod>/Settings.json`, a JALib settings envelope with a root `Setting` for the shared resource pack and `Feature.<feature>.Setting` entries. Keyviewer data is under `Feature.KeyViewer.Setting`. `KeyCount.dat` is separate and is not evidence of historical input in a submitted run.
- DMNote registration reads the supplied exported JSON, not its running desktop state. Stable 2.0.2 has key, stat, graph, and knob elements; there is no separate sprite element. Images are layers on those elements.

## Compatibility details

DMNote tabs are represented by maps such as `keys`, `keyPositions`, `statPositions`, `graphPositions`, and `knobPositions`. The registration contract accepts exactly one exported tab. Multiple tabs produce an actionable error instead of guessing which tab the user intended.

Embedded local images use an ID and Base64 bytes, with `dmnote-local-image://<id>` references. The actual saved field names and fonts must be resolved by source-specific import adapters. References to missing required assets prevent registration. Sound and JavaScript/plugin fields are omitted.

DMNote CSS selection depends on global enablement, the selected tab's enablement, and tab override content. The independent implementation must preserve that precedence. Useful selectors describe key/graph/knob roots, active state, label, counter and image layers; styles apply only to the imported scene. Arbitrary network loads and scripts are not part of the format.

DMNote graphs maintain history in 100 ms steps, with approximately 150 ms interpolation and a 500–5000 ms configurable history window. Knob rotation depends on HID axis input. DMNote supports `MOUSE1`–`MOUSE5` and HID bindings, while the current TUF recording contains keyboard input only. Absent measurements must not become invented events or values.

## Primary references

- [Jipper settings](https://github.com/Jongye0l/JipperResourcePack/blob/0ba3ec08cb650c3d848b544cc6d0bde943730b5f/JipperResourcePack/KeyViewerContents/KeyViewerSetting.cs)
- [DMNote preset fields](https://github.com/DmNote-App/DmNote/blob/4b4d6c2226efd220844dd7516d06618b9383c64c/src-tauri/src/commands/preset/mod.rs)
- [DMNote CSS selection](https://github.com/DmNote-App/DmNote/blob/4b4d6c2226efd220844dd7516d06618b9383c64c/src/renderer/hooks/app/useCustomCssInjection.ts)
- [DMNote key/mouse names](https://github.com/DmNote-App/DmNote/blob/4b4d6c2226efd220844dd7516d06618b9383c64c/src/renderer/utils/core/KeyMaps.ts)
