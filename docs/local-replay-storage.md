# Docker Desktop storage and CDN for live E2E

Start infrastructure once (alongside the existing development PostgreSQL and Redis):

```sh
./scripts/run.sh live-infra up
```

Docker Desktop then manages `replay-objects` and `replay-cdn`. Both restart unless
explicitly stopped. Their object data and CDN cache live in named Docker volumes.
`bun run e2e:live` never creates, stops or deletes these services or their volumes.

The two addresses are in `tools/live-e2e/services.json`:

```json
{
  "objectStore": "http://127.0.0.1:9000",
  "replayCdn": "http://127.0.0.1:8787"
}
```

No Cloudflare login, production credentials or production bucket is required.
Development-only credentials are fixed by this infrastructure. If ports conflict,
set `REPLAY_OBJECTS_PORT` / `REPLAY_CDN_PORT` when starting Compose and update the
addresses above. Published ports bind only to loopback.

Run the existing preparation workflow if this is a fresh machine, then:

```sh
bun run e2e:live
```

Before application startup the runner checks storage/CDN availability, copies
existing `.data/storage` objects through the server's verified migration task
(retaining originals), and registers the mod's packaged default assets. These
steps are idempotent. Storage fallback is disabled so missing CDN objects cannot
be hidden by a successful local-file read. An unavailable service fails startup
with the command to start it; it never falls back to production storage.

The resulting data path is:

```text
Mod → local submission API → S3-compatible Docker object store
Player → local API for authorized manifest and signed URLs
Player → local CDN Worker → object store / persistent edge cache
```

Evidence (hits/native input/metadata), visual assets (fonts/images), and the
materialized keyviewer/overlay bundles use the same object keys, server upload
and signing code, and production `deploy/replay-cdn/worker.mjs`. The local Worker
runs in Miniflare/workerd. A development-only adapter maps its R2 read binding to
the local S3-compatible store (S3rver). Map archive fixtures continue using the
existing local archive server; this does not change the TUF map CDN.

This reproduces signed delivery, CORS, range requests, integrity checks and edge
cache behavior. It does not emulate Cloudflare's global network, account policy,
latency or every R2-specific behavior. Local HTTP support is limited to the
server's `local-game` environment and the player's development build with an
exact configured loopback CDN origin. Production continues requiring HTTPS.

The 17 MiB multipart smoke test also checks the actual Rust upload implementation.
OpenDAL's `executors-tokio` feature is enabled so its two concurrent upload parts
have a runtime executor (small single-part objects do not exercise this path).

## Select the code being tested

The runner prints the path and commit for each sibling application. Defaults are
`../tuf-backend`, `../t21c-web-frontend`, and `../adofai-web-editor`.
Use `E2E_TUF_BACKEND`, `E2E_TUF_FRONTEND`, or `E2E_WEB_ADOFAI` to select another
checkout, or save persistent selections in ignored
`tools/live-e2e/.data/repositories.json`:

```json
{ "editor": "/absolute/path/to/current/web-adofai" }
```

The player checkout must include direct replay CDN support and the
`VITE_LOCAL_REPLAY_CDN_ORIGIN` development option. Existing checkouts and their
uncommitted edits are not switched or overwritten.

## Commands

```sh
./scripts/run.sh live-infra status   # container health
./scripts/run.sh live-infra check    # upload / delivery / cache / authorization smoke checks
./scripts/run.sh live-infra prepare  # migrate old files and seed defaults without starting apps
./scripts/run.sh live-infra stop     # stop only the two storage services; retain data
```

Re-run `up` after changing local infrastructure or the production Worker; it
rebuilds the image while retaining volumes. Do not use `docker compose down -v`
unless you intend to delete development database and storage volumes.
