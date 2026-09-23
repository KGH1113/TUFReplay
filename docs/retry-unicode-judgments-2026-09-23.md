# Retry recording, Unicode preset names, and replay judgments

## Recording retries

The installed game's `scrController.Fail2_Update` starts `ResetCustomLevel`, which calls
`scnGame.ResetScene` and `Play` without returning through `scnEditor.Play`.
`OnMusicScheduled` may enter `PlayerControl` directly when countdown is disabled.
Recording preparation previously ran only on `Countdown`/`Checkpoint`.

All three capture entry states now share an idempotent preparation path. Entering
`PlayerControl` after countdown keeps the existing capture; direct entry prepares
the new attempt first. A terminal failed attempt is replaced even if activity
persistence never produced a run draft. Pending failed-hit finalization happens
before the retry resets its evidence. Replay playback and recording guards remain active.

## Unicode

`@adofai-ipc/client` 0.4.1 sends JSON without a charset. AdofaiIpc's listener constructs
its reader with `HttpListenerRequest.ContentEncoding`, which can select the Mono
default encoding. The companion fetch adapter now declares `application/json;
charset=utf-8` for IPC JSON calls, preserving names, preset text, and asset paths.
Tests exercise Korean, emoji, nested JSON, Request bodies, headers, cancellation,
legacy response normalization, and non-IPC passthrough. Names already stored as
question marks have lost their original characters and must be entered again.

## Native judgment evidence and implementation

Inspected installed `Assembly-CSharp.dll` with ilspycmd: `scrHitTextManager`,
`scrHitTextMesh`, `scrPlanet.SwitchChosen`, `scrPlayer.OnDamage/ClearMisses`,
`scrMissIndicator`, `RDUtils`, and `GCS`. Inspected DOTween's `Punch` implementation.
AssetRipper 1.3.14 ran headless against the installed game's Data folder, including
Managed assemblies for MonoBehaviour decoding.

- `resources.assets` HitTextMesh 5478 / script 12649: scale 0.1, punch 0.03,
  duration 0.15, vibrato 5, elasticity 1. DOTween resolves this into 0.05s and 0.10s
  OutQuad steps. TMP 13588 uses godoMaum, size 50 (55 for English).
- Text is held for 0.5s and fades for 0.7s; the pooled object expires at 1.25s.
  It appears one Unity unit above the hit planet along the hit-time camera up vector.
- XPerfect border TMP 14508 uses material 47 and `xperfect_border` texture.
  The initial browser implementation incorrectly emitted stars on every XPerfect.
  Native emission is gated by `IsShowXPerfect(onlyWithParticle: true)`; the presence
  of a particle prefab does not imply that every XPerfect should emit it.
  The browser now renders XPerfect text and border without star particles.
- Miss prefab 6090 uses sprite 4925 (`miss_indicator`, 148×148, 330 PPU), tinted
  #fc4d4d. Failed/no-fail marker prefab 6089 uses sprite 4430 (`fail_indicator`).
  Misses remain at the failed input position until a successful hit clears them;
  failure markers blink for 3s. Their fade duration is 0.6s; default InFlash
  resolves to InQuad because SetEase truncates the default amplitude to 1.
  Sprite tint approaches transparent white during this fade, as in DOColor.
- Current English strings were read from `Localization/Auto/Translations`.
  Modern replays use native `XPerfect`, `Early!!`, `-Perfect`, etc.; legacy
  replays retain their older labels, including `Too Early!`.

The sibling web player separates pure judgment timing, hit-position sampling, and
canvas painting. Accepted hits use the landing position; rejected hits use the
orbiting planet. Saved effective pitch converts song time to animation seconds.
Hide actions persist per floor, and auto/midspin judgments stay hidden. Judgment
painting runs even without a selected keyviewer or overlay. Assets and font load
before the replay is ready; disposal releases the font and image caches.

Asset regeneration: in the web-player repository, load the game Data folder in
AssetRipper headless and run `node scripts/import-assetripper-judgments.mjs URL`.
`public/assets/judgments/provenance.json` records source names, dimensions and hashes.

## Verification

- `./scripts/run.sh mod-check`: native shim, C# build, Unity/Mono compatibility,
  C# suites and 13 updater tests passed; installation skipped.
- `./scripts/run.sh web-check`: 153 tests, application/test TypeScript, Biome and
  production companion build passed.
- Player unit suite: 983 passed, one existing sample-dependent skip. Seven new
  judgment tests cover timing, overlap, pitch, seeking, Hide, landing positions,
  independent miss sprites, camera projection/rotation, and XPerfect text/border
  without unsolicited particle emission.
- Player TypeScript and production build passed. Changed-file Biome checks have
  no errors (one pre-existing non-null assertion warning in the shared renderer).
- Manual game/browser verification is left to the user, as requested. No deployment.
