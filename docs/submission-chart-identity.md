# Automatic submission chart identity

Automatic registration now requires the recorded chart's submission identity to
match the official chart selected by the server. This is an additional check in
the trusted-tester adapter; it is not an independent simulation or proof of the
actual chart or input used by a potentially modified client.

## Admission before upload

Every `run_start` and REST issuance request now requires
`submission_gameplay_hash_version: 1` and a lowercase 64-character
`submission_gameplay_hash_hex`. The mod computes this from the in-memory game
chart at each attempt's input-capture boundary, including editor changes that
were never saved to disk. The immutable per-attempt hash is reused only when
reconnecting that same run.

The server checks official eligibility, selects the official original, requires
the current file ID and compares its submission hash **before** inserting a run,
submission record, or Redis upload session. Missing/unsupported hashes, ambiguous
official selection, mismatch, outdated files, and unavailable references fail
closed. No input/hit chunks or per-run artifacts are uploaded for those attempts.
Ordinary authentication/rate-limit/connection state and shared official-chart
cache are independent of player evidence.

A level socket may warm the public official reference without creating a run.
The admission lookup has an eight-second deadline; the client's ten-second
approval window and 8,192-record buffer remain bounded. A cold archive may finish
warming for a later attempt. The client sends evidence only after `ready` carries
`chart_verified: true`, so a new mod cannot silently upload to an old server that
does not implement admission. The game thread never waits for a network response;
runtime hashing itself happens synchronously on the game thread before native
input capture starts.

Successful admission is stored atomically with the run as `chart_admission`.
Reconnect claims must match it; final trusted-tester validation checks uploaded
metadata against it before checking the current official reference again.
Old unsealed REST sessions without admission cannot resume upload. Previously
sealed evidence and published replays are preserved.

Local activity only exposes a submission run ID after approval. If a short clear
is saved first, a synchronized late-approval callback links that existing local
record; denial leaves Submit disabled. A preflight rejection remains observable
even if capture already completed. The game displays a rejection notification,
and normal local replay recording remains independent.

Deploy the server migration and updated mod together. Existing clients without
the new hash fields are rejected before run creation; this is intentional.

The local E2E preparer downloads the official original ZIP together with its
metadata and seeds both `levels` and `cdn_files` in the dedicated local TUF DB.
It never constructs the server's reference from an edited installed folder.
Re-run `./scripts/run.sh live-infra charts` for manifests created by the older
preparer; startup rejects manifests that lack official metadata.

The local runner serves the matching `/cdn/{fileId}/metadata` response alongside
the official ZIP on port 5152. `tuf_metadata_base_url` selects that service; TUF
catalog, identity and registration requests continue to use port 3002. The local
TUF API does not mount the separate CDN service's routes, so seeding `cdn_files`
alone is insufficient. When `tuf_metadata_base_url` is omitted, production keeps
using the TUF API origin for metadata as before. Restart `bun run e2e:live` after
changing this configuration and record a new run; rejected historical attempts
have no uploaded evidence to recover.

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
For sprite-style charts, `LevelData.Decode` retains `pathData` instead of producing
`angleData`; the hash writes a distinct path marker and the exact loaded path.
This leaves existing modern chart hashes unchanged while detecting legacy path edits.

Included gameplay data:

- All tile angles and tile count, or the legacy sprite path and its length.
- BPM, song offset, countdown ticks and separate-countdown mode.
- SetSpeed type/value/angleOffset, Twirl, Hold duration, MultiPlanet, Pause
  duration/countdown/angle correction, AutoPlayTiles enabled/safetyTiles,
  ScaleMargin, Multitap, and FreeRoam geometry/timing events.

Color, camera and ordinary decoration data do not enter the hash. A TUFPLAY color
variant or a chart with only ordinary decorations removed therefore retains its
identity. BPM changes, excerpts, changed judgment margins, autoplay changes and
other supported gameplay edits change the identity.

Unknown action types, scripted actions (`CallMethod`, `AddComponent`,
`SetInputEvent`, `KillPlayer`), and active gameplay hitboxes are not silently
treated as visual data. These are unsupported and cannot
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
