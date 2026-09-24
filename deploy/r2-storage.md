# R2 artifact storage and visual assets

The artifact backend is selected with `TUF_REPLAY_STORAGE=local|r2`. R2 uses the
account S3 endpoint, region `auto`, and a bucket-scoped Object Read & Write token.
The bucket stays private. Credentials only exist in the protected host
`/home/kgh/tuf-replay-data/auto-submission/r2.env` (mode 600), never browser bundles.
Use `deploy/r2.env.example` for its four keys. Set `TUF_REPLAY_STORAGE=r2` and
`R2_LOCAL_FALLBACK=true` in the existing protected `.env` to enable it at the next
verified deployment. The credentials file alone does not switch the application.

## Deployment and migration

Use the existing deployment workflow. It checks R2 with a disposable random
object (PUT, GET, SHA-256, DELETE), backs up PostgreSQL before schema migration,
and deploys the tested reader. After local/public readiness succeeds it runs:

- `task migrate_artifacts`: stream local objects to R2, compare complete SHA-256
  and byte lengths, validate evidence content-addressed names, retain originals.
- `task migrate_visual_assets`: process one legacy preset at a time, verify its
  stored checksum, write/verify each asset, then atomically replace only the
  bundle metadata/hash/size. Preset IDs, names and run selections remain intact.

Both tasks are resumable. Logs and backups stay in the protected host state
directory. A conversion failure leaves the newly deployed compatible reader
running, fails the workflow and permits a retry; it does not revert to an old
binary that cannot read schema 2. The migration does not delete local objects or
the database backup. Missing R2 objects can fall back to the local copy; permission,
network and other R2 errors are surfaced rather than hidden. Explicit object
deletions remove the fallback first, preventing deleted evidence from reappearing.

For rollback, retain an application version supporting both bundle schemas and
the R2 backend. Switching to an old local-only binary would hide new R2 writes.
A full storage rollback requires copying and verifying new R2 objects locally
before changing the backend. Do not restore a database snapshot over new live
submissions. Keep local fallback enabled until a separate retention decision.

## Upload and replay contracts

The mod computes SHA-256, batches `POST /api/v1/visual-assets/check` (128 references
per request), and sends only missing bytes with authenticated
`PUT /api/v1/visual-assets/{sha256}`. Individual assets are limited to 48 MiB;
the preset's decoded asset total remains 128 MiB. The final preset POST contains
schema 2 references (`path`, `sha256`, `bytes`, `media_type`), without base64.
Retries reuse verified uploads. A private digest alone cannot grant another
user access: they must provide matching bytes. Hashes deduplicate physical storage.

New preset metadata is kept in PostgreSQL; binaries are objects. Old inline
uploads remain accepted and are externalized on the server. Old players receive
schema 1 bundles; the updated player requests `?format=3&asset_mode=objects` and
fetches separately verified binaries. Existing evidence URLs and the published-run
gate are unchanged. Internal server-receipt evidence is never a public file kind.

Private visual assets are served only for their owner or a published replay that
references them, with `private, no-store`. Arbitrary external URLs are not accepted.
The player verifies byte counts/hashes and shares downloaded digests between
keyviewer and overlay. The user-facing gateway permits 128 MiB bodies, matching the
internal gateway; proxy 413 errors are reported as a preset-size error in the mod.

## Rights-cleared common assets

An operator can use `task publish_visual_asset sha256:<digest> license_url:<https-url>`
after verifying redistribution rights. This verifies the object before recording
the license source. There is no user API for setting public status. Publication
enables `/api/v1/visual-assets/public/{sha256}` with immutable public caching;
object-mode metadata advertises that exact route, and the player caches it across
replays. Configure a CDN cache rule only for this public prefix, never the entire
API or bucket. The route works as a shared HTTP cache origin without that rule.

Bundled game/mod assets are not automatically made public based on their filenames.
Until an asset's rights are reviewed, it remains in private storage and is still
deduplicated for its owner. Public immutable caches are appropriate only for assets
approved for permanent redistribution.

## Future large private recordings

The current asset API is bounded binary upload; it is not the upload API for future
microphone or camera recordings. Evidence writes already stream to S3 multipart
writers in 8 MiB chunks with two concurrent parts and abort on errors. A future
recording uploader should create an owner-bound upload session with a random
private staging key, expected size/hash, expiry and part ledger; issue short-lived
part URLs; resume only acknowledged parts; and complete only after server-side
integrity verification. Bind final objects to the same authorization/publication
state machine and clean expired incomplete sessions/multipart uploads. Never let
clients write final content-addressed evidence keys or make private recordings
public through the common-asset registry. No recording feature is introduced here.
