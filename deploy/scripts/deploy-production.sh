#!/usr/bin/env bash
set -euo pipefail

# Internal helper for .github/workflows/deploy.yml. Production deployments are
# initiated through GitHub Actions, which supplies the revision it verified.
if [[ "$#" != "1" || ! "$1" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Start deployment through the Deploy GitHub Actions workflow on main." >&2
  exit 1
fi
DEPLOY_SHA="$1"

DEPLOY_DIR="/srv/TUFReplay"
ENV_FILE="$DEPLOY_DIR/.env.production"
SERVICE_UNIT="tuf-replay-web.service"
USER_SYSTEMD_DIR="$HOME/.config/systemd/user"
UNIT_SOURCE="$DEPLOY_DIR/deploy/systemd/$SERVICE_UNIT"
UNIT_TARGET="$USER_SYSTEMD_DIR/$SERVICE_UNIT"
USER_RUNTIME_DIR="/run/user/$(id -u)"

if [[ -z "${XDG_RUNTIME_DIR:-}" && -d "$USER_RUNTIME_DIR" ]]; then
  export XDG_RUNTIME_DIR="$USER_RUNTIME_DIR"
fi

require_command() {
  command -v "$1" >/dev/null 2>&1 || {
    printf 'Missing required command: %s\n' "$1" >&2
    exit 1
  }
}

require_command docker
require_command systemctl
require_command python3
require_command curl
require_command stat
require_command git

cd "$DEPLOY_DIR"

if [[ "$(git rev-parse HEAD)" != "$DEPLOY_SHA" ]]; then
  echo "Checkout does not match the revision verified by the deployment workflow." >&2
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing .env.production; create it from deploy/production.env.example before deployment." >&2
  exit 1
fi

env_mode="$(stat -c '%a' "$ENV_FILE")"
if [[ "$env_mode" != "600" && "$env_mode" != "400" ]]; then
  echo ".env.production permissions must be 600 or 400." >&2
  exit 1
fi

python3 deploy/scripts/validate-production-env.py "$ENV_FILE"

compose() {
  docker compose \
    --env-file "$ENV_FILE" \
    -f docker-compose.yml \
    -f deploy/docker-compose.production.yml \
    --profile production \
    "$@"
}

if ! compose config --quiet; then
  echo "Production Compose configuration is invalid; values were not printed." >&2
  exit 1
fi

export BUILD_SHA="$(git rev-parse --short=12 HEAD)"
compose build tuf-replay-server tuf-replay-web
compose up -d --wait postgres-production redis-ingest-production

if ! compose run --rm --no-deps tuf-replay-server db migrate >/dev/null 2>&1; then
  echo "Database migration failed; command output was withheld because it may contain connection details." >&2
  exit 1
fi
echo "Database migrations completed."

if [[ ! -f "$UNIT_SOURCE" ]]; then
  echo "Missing systemd unit: $UNIT_SOURCE" >&2
  exit 1
fi

mkdir -p "$USER_SYSTEMD_DIR"
install -m 0644 "$UNIT_SOURCE" "$UNIT_TARGET"
systemctl --user daemon-reload
systemctl --user enable "$SERVICE_UNIT"
systemctl --user restart "$SERVICE_UNIT"
systemctl --user is-active "$SERVICE_UNIT" >/dev/null

ready=0
for attempt in {1..30}; do
  if curl --fail --silent --show-error --max-time 5 http://127.0.0.1:5150/_readiness >/dev/null; then
    ready=1
    break
  fi
  sleep 2
done

if [[ "$ready" != "1" ]]; then
  echo "Rust API readiness check failed at http://127.0.0.1:5150/_readiness." >&2
  compose ps
  exit 1
fi

echo "Rust API, database, and queue readiness check passed."
compose ps
