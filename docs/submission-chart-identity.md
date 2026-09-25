# Automatic submission chart identity

Automatic registration now requires the recorded chart's submission identity to
match the official chart selected by the server. This is an additional check in
the trusted-tester adapter; it is not an independent simulation or proof of the
actual chart or input used by a potentially modified client.

## Selecting the official original

The public level response provides `fileId` and `dlLink`, but no authoritative
chart path. The server also reads `/cdn/{fileId}/metadata`:

- Only `pathConfirmed: true` makes `targetLevel` authoritative. TUF's automatic
  largest-file selection has `pathConfirmed: false` and is not trusted.
- `targetLevel` is a storage path. Exactly one `levelFiles` entry must reference
  it; that entry's **map key** identifies the original ZIP path. The flattened
  `targetLevelRelativePath`, client path, and basename are not selection rules.
- If no path is confirmed (including older catalogs returning metadata 404),
  every `.adofai` in the original archive must produce the same submission hash.
  Different gameplay hashes produce `official_chart_ambiguous` and no automatic
  registration. A moderator must resolve the source; there is no largest-file or
  client-selection fallback.
- An unavailable metadata endpoint is a retryable upstream error, not permission
  to assume that the archive is unambiguous.
- Original archive directories are preserved for this selection. Existing
  TUFHelper-style flattened paths are not used to establish authority.

On 2026-09-25 the public metadata for TUF #8068 confirmed
`Merry Christmas/levelEX.adofai` through this mapping. Its archive also contains
`Merry Christmas/level.adofai`; choosing that other client path cannot change the
server's reference chart.

## Submission hash v1

`server/contracts/submission-gameplay-v1.json` is embedded by both C# and Rust. The hash
has its own domain prefix and version, separate from activity hash v4, the mod's
existing replay semantic hash, and the server's persisted replay hash v1.

The SHA-256 payload uses big-endian integers, IEEE float32 values, length-prefixed
UTF-8 strings, and explicit counts. Angles normalize modulo 360 except midspin
999. Events sort by floor and event name, preserving same-kind order on a floor.
Numeric enum encodings normalize to the game enum names. The server converts
modern `pathData` to angles with the game's `FloorHelper.MigratePathData` mapping.
The game's v9–v16 Pause compatibility transformation is applied before hashing.

Included gameplay data:

- All tile angles and tile count.
- BPM, song offset, countdown ticks and separate-countdown mode.
- SetSpeed type/value/angleOffset, Twirl, Hold duration, MultiPlanet, Pause
  duration/countdown/angle correction, AutoPlayTiles enabled/safetyTiles,
  ScaleMargin, Multitap, and FreeRoam geometry/timing events.

Color, camera and ordinary decoration data do not enter the hash. A TUFPLAY color
variant or a chart with only ordinary decorations removed therefore retains its
identity. BPM changes, excerpts, changed judgment margins, autoplay changes and
other supported gameplay edits change the identity.

Unknown action types, scripted actions (`CallMethod`, `AddComponent`,
`SetInputEvent`, `KillPlayer`), active gameplay hitboxes, and legacy sprite-style
charts are not silently treated as visual data. These are unsupported and cannot
auto-register, even if the official file uses them. This is an explicit supported
chart boundary rather than evidence that a submitted run is fraudulent. General
modification of game behavior by external mods remains outside this identity
check.

## Capture, validation and caching

At level preparation, the mod reads the game's loaded `LevelData`, computes the
hash once, and copies the value into each run payload. Immediate retries reuse
that value. Leaving the session invalidates it; another editor play or file load
prepares a fresh value. Existing activity/replay hashes are calculated and stored
as before. No official archive download blocks the game from starting a run.

Run evidence metadata carries:

```json
{
  "submissionGameplayHashVersion": 1,
  "submissionGameplayHashHex": "<64 lowercase hexadecimal characters>"
}
```

The server compares these claims after the evidence integrity and recorded-result
checks and before invoking the registrar. Missing versions, unsupported versions,
malformed hashes and mismatches terminate registration with a specific reason.
An activity `gameplayHashHex` cannot substitute for this field.

The catalog caches up to four official charts and their computed hashes (each
chart is bounded at 32 MiB). Every acquisition re-resolves current TUF metadata;
a different level, file ID, download URL or confirmed path misses the cache.
Metadata is checked again after hydration so an upstream revision change cannot
publish a stale selection. The submission hash version is process-constant and
deploying a new contract resets this memory cache. Temporary ZIP/extraction files
are removed after hydration.

## Rollout and verification

Both the mod and automatic-submission server need this change. Old run evidence
without the new fields is deliberately rejected; old published replays remain
readable. This work does not migrate or relabel previous published submissions.

Checks performed:

- C# build, Unity/Mono compatibility checks and the existing C# suites, including
  shared C#/Rust golden vectors.
- Rust format/all-target checks and library tests: ordinary visual variants,
  gameplay mutations, legacy path/Pause normalization, numeric enums, original
  ZIP paths, ambiguity, and confirmed metadata mapping.
- Isolated PostgreSQL integration tests, including proof that missing, changed,
  and unsupported identities never call the registration adapter.
- Public TUF metadata inspected read-only; game contracts inspected with
  `ilspycmd` and existing native decompilation (`LevelData.Decode`,
  `LevelEvent.Decode`, `FloorHelper`, `scnGame`).

Final local results: all C# suites passed; Rust library tests **55 passed**;
PostgreSQL integration tests **28 passed, 1 existing visual-fixture test ignored**;
Clippy with warnings denied passed; CSharpier checked 298 files successfully;
33 shell scripts passed syntax checks and the SSH-stdin regression test passed.
Shellcheck itself was not installed. These verification commands did not install
the changes into the game or deploy them to production.

Actual game/browser verification belongs to the user. No game installation or
production deployment is performed by the verification commands.

```sh
TUFREPLAY_BUILD_FLAVOR=auto-submission ./scripts/run.sh mod-check
./scripts/run.sh server-check
DATABASE_URL=postgres://.../ISOLATED_TEST_DB ./scripts/run.sh server-check --integration
```
