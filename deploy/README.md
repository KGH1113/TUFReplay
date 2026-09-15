# Home-server deployment

The home-server Compose stack keeps the existing Vite web preview and adds the
Rust API, Loco worker, and scheduler. Production uses the root
`docker-compose.yml` plus `deploy/docker-compose.production.yml`; developers can
continue using the root file by itself with its current Postgres and Redis
defaults.

The production overlay profiles out the development database and Redis, then
creates separate persistent volumes for production Postgres, Redis ingest, and
the local artifact store. Postgres and Redis have no host-published ports. The
Rust container is non-root and publishes only `127.0.0.1:5150`; the existing
web preview remains on `127.0.0.1:4174`.

## Required environment

Copy `deploy/production.env.example` to `/srv/TUFReplay/.env.production` on the
home server, fill the blank fields using a password manager, and set file mode
to `600`. Do not commit the real file or paste its values into logs or chat.
Generate independent URL-safe random values for the Postgres password and both
internal service tokens. The two tokens must differ; each token must be 32–512
bytes and contain no whitespace. `deploy/scripts/validate-production-env.py`
reports only variable names and whether each is configured; it never prints a
value.

The Compose overlay fixes these server-side origins:

- `TUF_WEB_ORIGIN=https://tuforums.com` for the TUF frontend.
- `SUBMISSION_WEB_ORIGIN=https://tufreplay.impl1113.dev` for the companion web
  app and OAuth callback.
- `TUF_API_BASE_URL=https://api.tuforums.com` for TUF API requests.

Set `SUBMISSION_VALIDATION_MODE=trusted_tester` only after the TUF backend has
its allowlist policy configured. Rust's trusted-tester mode does not grant
account permission. TUF remains the only allowlist authority; its
`AUTO_SUBMISSION_ENABLED` setting defaults to false, and an empty
`AUTO_SUBMISSION_TRUSTED_USER_IDS` denies everyone. Add the TUF account UUID to
that allowlist, not the linked player ID.

The TUF backend also needs:

- `AUTO_SUBMISSION_ENABLED=false` during initial setup
- `AUTO_SUBMISSION_TRUSTED_USER_IDS=670cac2c-8175-46a6-87f7-b92741d4499f`
  for the first tester (`impl.dev`, linked player `7410`)
- `AUTO_SUBMISSION_API_URL=https://tufreplay.impl1113.dev`
- `TUF_TO_AUTO_SUBMISSION_TOKEN` and `AUTO_SUBMISSION_TO_TUF_TOKEN`, matching
  the two independently stored Rust service tokens
- `TUF_AUTO_SUBMISSION_OAUTH_CLIENT_ID=1dc9ff206f5301c9e7ef4ba9b209c7c7`
- OAuth redirect URI `https://tufreplay.impl1113.dev/oauth/callback`

Configure those TUF-side values through its own secret/configuration process.
This repository's workflow does not access or change the TUF production server.

## Nginx routing

The API reuses the existing `tufreplay.impl1113.dev` hostname, so the current
exact Cloudflare Tunnel hostname remains sufficient. Merge
`deploy/nginx/tufreplay-api-locations.conf` into the existing Nginx server block
for that hostname, before the web SPA fallback. It sends `/api/v1/`,
`/internal/`, and the exact `/_readiness` path to `127.0.0.1:5150` and forwards
WebSocket upgrade headers. Keep `/api/tuf/*` and `/oauth/callback` on the web
upstream at `127.0.0.1:4174`.

Nginx changes are a separate host operation. Before reloading Nginx, inspect the
merged server block and run `nginx -t`. The deployment workflow does not edit or
reload Nginx.

## Deployment sequence

The existing `tuf-replay-web.service` unit name is preserved. Its Compose
project is the same project as the current web preview, so an update reuses the
existing web container and port binding. Production uses three separate
persistent volumes:

- `tuf-replay-production-postgres` for the application database and Loco job
  queue.
- `tuf-replay-production-redis` for ingest-session AOF data.
- `tuf-replay-production-artifacts` mounted at
  `/var/lib/tuf-replay/artifacts` for downloaded chart and evidence artifacts.

The deploy workflow runs Rust formatting, lint, tests, and both web and Rust
container builds before connecting to the home server. On the server it checks
that `.env.production` exists with restrictive permissions, validates the
required values without displaying them, checks the merged Compose model, builds
the images, waits for healthy Postgres and Redis, and applies Loco migrations as
a separate command. Only after the migration succeeds does it restart the
systemd unit. The service starts `tuf_replay_server-cli start --all` with
`SCHEDULER_CONFIG=config/scheduler.yaml`, so HTTP, worker, and scheduler share
one container.

The API healthcheck calls Loco's `/_readiness`, which checks the database and
Postgres queue. Postgres and Redis have their own healthchecks. A healthy HTTP
readiness response does not report scheduler job progress; after startup, check
the Rust service's startup logs for the configured reconciliation and cleanup
jobs. Never use `docker compose down -v`: it deletes the persistent databases,
Redis data, and artifact volume.

Production deployments must run through the **Deploy (Guhyeon Kang's Home
Server)** GitHub Actions workflow. After the environment and initial Nginx
routing have been provisioned, merge the reviewed change into `main` to trigger
the workflow. To rerun deployment of current `main`, use the workflow's **Run
workflow** button with branch `main`, or:

```sh
gh workflow run deploy.yml --ref main
```

`deploy/scripts/deploy-production.sh` is an internal workflow helper, not a
manual SSH deployment entry point. It requires the tested revision supplied by
the workflow and refuses a different checkout. Do not run Compose updates,
production migrations, or application service restarts manually over SSH.

The GitHub workflow currently deploys on pushes to `main` and manual dispatch.
The workflow requires `.env.production` before fetching or resetting the
checkout. It deploys only the exact commit that passed its checks, and stops
if `main` advanced while those checks were running. It streams the validator
from that tested revision and runs it before changing the checkout.
Do not trigger a production deployment until the TUF allowlist, secrets, and
Nginx route are ready and the user has authorized the rollout.

## Local preflight

From the repository root, inspect the Compose model without printing its
expanded environment:

```sh
docker compose \
  --env-file .env.production \
  -f docker-compose.yml \
  -f deploy/docker-compose.production.yml \
  --profile production config --quiet
```

After deployment, the API is available to host-local checks at
`http://127.0.0.1:5150/_readiness`; the public route is
`https://tufreplay.impl1113.dev/_readiness` after the Nginx change.
