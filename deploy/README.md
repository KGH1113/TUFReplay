# Home-server deployment

Web and Rust deployments run only through GitHub Actions. Mod packages are built locally and uploaded by the operator; CI does not receive game DLLs or publish mod releases automatically.

| Branch | Compose project | Public hostname | Loopback gateway |
| --- | --- | --- | --- |
| `main` | `tufreplay-main` | `tufreplay.impl1113.dev` | 4174 |
| `dev` | `tufreplay-dev` | `tufreplay-dev.impl1113.dev` | 4175 |
| `feat/auto-submission` | `tufreplay-auto` | `tufreplay-auto.impl1113.dev` | 4176 |

Each environment uses `deploy/docker-compose.<environment>.yml` and a checkout under `/home/kgh/tuf-replay-environments/<environment>`. Main and dev run web plus Nginx gateway. Auto-submission also runs the Rust API, worker, scheduler, PostgreSQL, Redis, and persistent artifact storage. The root Compose file remains for local development.

## Host setup

The protected auto environment is `/home/kgh/tuf-replay-data/auto-submission/.env`, mode `600`. Use `auto-submission.env.example` as the variable list. Database passwords and the two internal tokens must be independently generated URL-safe values; tokens must differ. Keep this file outside the Git checkout and do not put its contents in logs or GitHub artifacts.

`validate-auto-environment.py` checks configuration without printing secret values. Compose runtime metadata lives in `/home/kgh/tuf-replay-data/<environment>/compose.env`; the workflow writes it with mode `600`. Database, Redis, and artifact volumes belong only to the auto Compose project and have no host-published database ports. Never use `docker compose down -v` for deployment or recovery.

The user performs the one-time sudo installation in `host/bootstrap-routing.sh`. It installs a root-owned helper restricted to the fixed dev and auto hostnames, plus a sudo rule for that exact no-argument command. Installing it does not alter routes. The deployment workflow invokes the helper after local readiness; it adds the Nginx and Cloudflare Tunnel routes, validates configuration, and reloads services. Existing main routing remains in place.

Required GitHub secrets are `TS_OAUTH_CLIENT_ID` and `TS_OAUTH_SECRET`. The Tailnet policy must permit ephemeral `tag:gh-runner` nodes to reach `kgh` with Tailscale SSH.

## Deployment workflow

Push to the corresponding branch, or dispatch `deploy.yml` on that branch. Source checks are required before deployment; the auto branch additionally runs Rust formatting, Clippy, and tests with PostgreSQL/Redis. Linux/amd64 images are built from the tested commit and transferred through Tailscale. Host credentials stay on the home server.

The helper checks that the tested commit remains the branch tip. Environment/commit image tags already present on the host are reused on reruns; new tags are loaded only when missing. A new commit is required to rebuild a deployed tag against updated base images. Services require prebuilt images, including on reboot.

For the first main migration, the workflow stops the old `tuf-replay-web.service` only after the new images are available. It then starts the isolated main stack on port 4174. Failed switches attempt to restore the previous checkout, Compose environment, images, unit, and active state; the first main migration restores the legacy service. Recovery is best-effort. Successful database migrations are retained, so schema changes must remain compatible with the previous API for rollback to work.

`/deployment.json` returns the deployed environment, build flavor, and Git SHA with cache prevention headers. The workflow verifies this response locally and through the public hostname. Auto-submission additionally exposes `/_readiness`; its Rust process runs `start --all` with scheduler configuration. Readiness checks database/queue availability, not a complete OAuth or in-game submission.

Do not run `deploy-home.sh`, Compose deployment commands, migrations, or application restarts manually over SSH. Use the workflow to preserve checks and deployment ordering.

## Manual mod packages

Build the standard package from the dev worktree using `./scripts/run.sh package`. Its source version is `0.2.0-beta.4`; upload `build/TUFReplay.zip` and `build/TUFReplay.update.json` to the matching GitHub prerelease yourself.

Build the separate auto package from `feat/auto-submission`:

```sh
TUFREPLAY_BUILD_FLAVOR=auto-submission \
TUFREPLAY_BUILD_VERSION=0.2.0-auto-submission.1 \
./scripts/run.sh package
```

Upload that ZIP and manifest into a private directory such as `/home/kgh/tuf-replay-data/auto-submission/incoming/auto-1`. Choose a new increasing version for each package; a published version cannot be replaced with different bytes. Keep the incoming directory private.

After upload, run **Promote uploaded auto-submission package** (`publish-auto-update.yml`) on `feat/auto-submission`, with `package_version` matching the manifest and `upload_id` matching the incoming directory name. It shares the auto deployment lock, checks the live deployment, validates ZIP size/hash/flavor/version, and publishes atomically. It does not build a mod or create a GitHub Release. `deploymentSha` records which web/API deployment was checked during promotion; it does not claim the ZIP source commit.

The gateway serves these files from `/home/kgh/tuf-replay-data/auto-submission/updates`:

- `/updates/auto-submission/latest.json`: current manifest, no cache.
- `/updates/auto-submission/versions/<version>/TUFReplay.zip`: immutable package.

There is no published update until the operator uploads and promotes one. The updater rejects cross-flavor packages and has no GitHub fallback for the auto channel. The web requires the auto mod flavor and submission protocol 2; its exact patch version is independent of the web deployment.

## TUF production integration

This repository's workflows do not access the TUF production server. TUF-side deployment, migrations, OAuth scope, and allowlist activation require their separate approved operating procedure.

The initial tester is `impl.dev` (player `7410`), account UUID `670cac2c-8175-46a6-87f7-b92741d4499f`. The official OAuth client ID is `1dc9ff206f5301c9e7ef4ba9b209c7c7`, with redirect `https://tufreplay-auto.impl1113.dev/oauth/callback`.

TUF BE needs `AUTO_SUBMISSION_API_URL=https://tufreplay-auto.impl1113.dev`, matching internal tokens, `TUF_AUTO_SUBMISSION_OAUTH_CLIENT_ID`, and the explicit `AUTO_SUBMISSION_ENABLED` rollout switch. Its default is off. Rust now also requires an active PostgreSQL `trusted_testers` row. After applying and populating the migration, set TUF BE `AUTO_SUBMISSION_TESTER_AUTHORITY=replay` to use that roster as the single tester source. Until then the default `environment` authority retains the old `AUTO_SUBMISSION_TRUSTED_USER_IDS` restriction in addition to Rust's DB check. The OAuth app still needs scope `65537`. See [tester administration rollout](trusted-testers-admin.md) for migration, restricted DB credentials and the localhost-only admin profile.

The Rust setting `SUBMISSION_VALIDATION_MODE=trusted_tester` deliberately skips the unfinished gameplay validator for authorized, active DB testers and records that validation was skipped. It does not grant tester access itself. A ready Replay deployment therefore does not mean TUF-side activation or a real in-game submission has been verified.
