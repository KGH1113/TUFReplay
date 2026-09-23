# Automatic installed-source import

Updated to the user's final choice on 2026-09-17: Jipper Resourcepack, Jipper KeyViewer, and ImplResourcePack use automatic mod detection, not uploaded configuration files. DMNote and Impl DMNote use exported JSON.

## Registration

- Select an installed source, enter a unique preset name, then register. TUFReplay reads that source's current saved settings and referenced assets automatically.
- Keyviewer and overlay registrations remain separate. Missing images/fonts can be attached when inspection requests them.
- If the source is not detected, install the mod, save its settings once and restart the game. The UI provides detection guidance and a retry action, not a settings file picker.
- Browser IPC omits configuration JSON for installed sources. Their adapters reject non-null configuration uploads, so stale forms cannot override the installed source.

## Discovery

UMM source folders are checked in the same Mods directory as TUFReplay. `Info.json` IDs identify installed sources regardless of folder naming. A trailing slash on the TUFReplay installation path is normalized before locating Mods. Unrelated game backup/profile folders are not traversed. Registration rereads the current saved files; it does not reuse settings from the earlier source discovery response.

## Verification

- `./scripts/run.sh web-check`: unit/contract tests, type checks, Biome and production build passed.
- Auto-submission `./scripts/run.sh mod-check`: build, Unity/Mono validation and full C# tests passed.
- Added C# coverage for renamed installed-source folders, trailing-slash paths, exclusion of a higher-version backup, foreign mod ID rejection, fresh settings after edits, and configuration upload rejection.
- Browser API contract verifies installed-source imports send no configuration JSON.

No game process was stopped and the running game's mod was not replaced. Install the built mod after exiting the game, then relaunch it to use the improved discovery. These tests establish detection/import behavior, not pixel equivalence for all renderer settings.
