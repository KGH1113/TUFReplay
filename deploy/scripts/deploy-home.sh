#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "$#" != 2 ]]; then
  echo "Usage: deploy-home.sh <main|dev|auto-submission> <full-git-sha>" >&2
  exit 64
fi

DEPLOY_ENVIRONMENT="$1"
DEPLOY_SHA="$2"
if [[ ! "$DEPLOY_SHA" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Deployment requires a full lowercase Git commit SHA." >&2
  exit 64
fi

case "$DEPLOY_ENVIRONMENT" in
  main)
    DEPLOY_BRANCH="main"
    PROJECT_NAME="tufreplay-main"
    PORT="4174"
    BUILD_FLAVOR="standard"
    DOMAIN="tufreplay.impl1113.dev"
    COMPOSE_FILE="deploy/docker-compose.main.yml"
    SERVICE_UNIT="tuf-replay-main.service"
    ;;
  dev)
    DEPLOY_BRANCH="dev"
    PROJECT_NAME="tufreplay-dev"
    PORT="4175"
    BUILD_FLAVOR="standard"
    DOMAIN="tufreplay-dev.impl1113.dev"
    COMPOSE_FILE="deploy/docker-compose.dev.yml"
    SERVICE_UNIT="tuf-replay-dev.service"
    ;;
  auto-submission)
    DEPLOY_BRANCH="feat/auto-submission"
    PROJECT_NAME="tufreplay-auto"
    PORT="4176"
    BUILD_FLAVOR="auto-submission"
    DOMAIN="tufreplay-auto.impl1113.dev"
    COMPOSE_FILE="deploy/docker-compose.auto-submission.yml"
    SERVICE_UNIT="tuf-replay-auto-submission.service"
    ;;
  *)
    echo "Unsupported deployment environment." >&2
    exit 64
    ;;
esac

DEPLOY_ROOT="/home/kgh/tuf-replay-environments"
DATA_ROOT="/home/kgh/tuf-replay-data"
DEPLOY_DIR="$DEPLOY_ROOT/$DEPLOY_ENVIRONMENT"
STATE_DIR="$DATA_ROOT/$DEPLOY_ENVIRONMENT"
COMPOSE_ENV_FILE="$STATE_DIR/compose.env"
REPO_URL="https://github.com/KGH1113/TUFReplay.git"
USER_SYSTEMD_DIR="$HOME/.config/systemd/user"
UNIT_SOURCE="$DEPLOY_DIR/deploy/systemd/$SERVICE_UNIT"
UNIT_TARGET="$USER_SYSTEMD_DIR/$SERVICE_UNIT"
USER_RUNTIME_DIR="/run/user/$(id -u)"
LEGACY_UNIT="tuf-replay-web.service"
MIGRATION_MARKER="$DATA_ROOT/main/legacy-main-migrated"
AUTO_ENV_FILE="$DATA_ROOT/auto-submission/.env"
AUTO_UPDATES_DIR="$DATA_ROOT/auto-submission/updates"

if [[ -z "${XDG_RUNTIME_DIR:-}" && -d "$USER_RUNTIME_DIR" ]]; then
  export XDG_RUNTIME_DIR="$USER_RUNTIME_DIR"
fi

require_command() {
  command -v "$1" >/dev/null 2>&1 || {
    printf 'Missing required command: %s\n' "$1" >&2
    exit 1
  }
}

for command_name in git docker systemctl curl python3 install stat flock; do
  require_command "$command_name"
done
docker compose version >/dev/null

umask 077
mkdir -p "$DEPLOY_ROOT" "$STATE_DIR"

exec 9>"$STATE_DIR/deploy.lock"
flock -n 9 || {
  echo "Another deployment is already running for this environment." >&2
  exit 1
}

COMPOSE_ARGS=(docker compose --project-name "$PROJECT_NAME" --env-file "$COMPOSE_ENV_FILE" -f "$COMPOSE_FILE")
if [[ "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
  COMPOSE_ARGS+=(--env-file "$AUTO_ENV_FILE")
fi
compose() {
  "${COMPOSE_ARGS[@]}" "$@"
}
fail_deploy() {
  printf '%s\n' "$1" >&2
  return 1
}

ROLLBACK_DIR="$(mktemp -d "$STATE_DIR/.rollback.XXXXXX")"
chmod 0700 "$ROLLBACK_DIR"
PREVIOUS_CHECKOUT_EXISTS=0
PREVIOUS_CHECKOUT_SHA=""
if [[ -d "$DEPLOY_DIR/.git" ]]; then
  PREVIOUS_CHECKOUT_EXISTS=1
  PREVIOUS_CHECKOUT_SHA="$(git -C "$DEPLOY_DIR" rev-parse HEAD)"
fi

PREVIOUS_COMPOSE_ENV_EXISTS=0
if [[ -f "$COMPOSE_ENV_FILE" ]]; then
  PREVIOUS_COMPOSE_ENV_EXISTS=1
  cp -p "$COMPOSE_ENV_FILE" "$ROLLBACK_DIR/compose.env"
fi

PREVIOUS_UNIT_FILE_EXISTS=0
if [[ -f "$UNIT_TARGET" ]]; then
  PREVIOUS_UNIT_FILE_EXISTS=1
  cp -p "$UNIT_TARGET" "$ROLLBACK_DIR/service.unit"
fi
PREVIOUS_UNIT_ACTIVE=0
PREVIOUS_UNIT_ENABLED=0
if systemctl --user cat "$SERVICE_UNIT" >/dev/null 2>&1; then
  if systemctl --user is-active --quiet "$SERVICE_UNIT"; then
    PREVIOUS_UNIT_ACTIVE=1
  fi
  if systemctl --user is-enabled --quiet "$SERVICE_UNIT"; then
    PREVIOUS_UNIT_ENABLED=1
  fi
fi

DEPLOYMENT_SWITCH_STARTED=0
DEPLOY_CHECKOUT_CREATED=0
DEPLOY_CHECKOUT_CLONED=0
COMPOSE_ENV_CHANGED=0
UNIT_CHANGED=0
STACK_TOUCHED=0
DB_MIGRATION_APPLIED=0
LEGACY_MIGRATION_PENDING=0
LEGACY_UNIT_FOUND=0
LEGACY_WAS_ACTIVE=0
LEGACY_WAS_ENABLED=0

on_deploy_error() {
  local status="$?"
  trap - ERR
  set +e

  if [[ "$DEPLOYMENT_SWITCH_STARTED" != "1" && "$COMPOSE_ENV_CHANGED" != "1" && "$UNIT_CHANGED" != "1" && "$STACK_TOUCHED" != "1" ]]; then
    rm -rf -- "$ROLLBACK_DIR"
    exit "$status"
  fi

  cd "$DEPLOY_DIR" 2>/dev/null || true
  if [[ "$STACK_TOUCHED" == "1" ]]; then
    compose down >/dev/null 2>&1 || true
  fi

  if [[ "$PREVIOUS_CHECKOUT_EXISTS" == "1" ]]; then
    git -C "$DEPLOY_DIR" checkout --detach --force "$PREVIOUS_CHECKOUT_SHA" >/dev/null 2>&1 || true
    git -C "$DEPLOY_DIR" reset --hard "$PREVIOUS_CHECKOUT_SHA" >/dev/null 2>&1 || true
    git -C "$DEPLOY_DIR" clean -ffdx >/dev/null 2>&1 || true
  elif [[ "$DEPLOY_CHECKOUT_CREATED" == "1" && ! -d "$DEPLOY_DIR/.git" ]]; then
    rm -rf -- "$DEPLOY_DIR"
  elif [[ "$DEPLOY_CHECKOUT_CREATED" == "1" && "$DEPLOY_CHECKOUT_CLONED" != "1" ]]; then
    rm -rf -- "$DEPLOY_DIR"
  fi

  if [[ "$PREVIOUS_COMPOSE_ENV_EXISTS" == "1" ]]; then
    cp -p "$ROLLBACK_DIR/compose.env" "$COMPOSE_ENV_FILE.rollback" 2>/dev/null &&
      mv -f "$COMPOSE_ENV_FILE.rollback" "$COMPOSE_ENV_FILE" || true
  else
    rm -f "$COMPOSE_ENV_FILE"
  fi

  mkdir -p "$USER_SYSTEMD_DIR"
  if [[ "$PREVIOUS_UNIT_FILE_EXISTS" == "1" ]]; then
    install -m 0644 "$ROLLBACK_DIR/service.unit" "$UNIT_TARGET" 2>/dev/null || true
  else
    rm -f "$UNIT_TARGET"
  fi
  systemctl --user daemon-reload >/dev/null 2>&1 || true

  if [[ "$PREVIOUS_UNIT_ENABLED" == "1" ]]; then
    systemctl --user enable "$SERVICE_UNIT" >/dev/null 2>&1 || true
  else
    systemctl --user disable "$SERVICE_UNIT" >/dev/null 2>&1 || true
  fi

  if [[ "$PREVIOUS_UNIT_ACTIVE" == "1" ]]; then
    if [[ "$STACK_TOUCHED" == "1" ]]; then
      compose up -d --no-build --wait --remove-orphans >/dev/null 2>&1 || true
    fi
    systemctl --user start "$SERVICE_UNIT" >/dev/null 2>&1 || true
  else
    systemctl --user stop "$SERVICE_UNIT" >/dev/null 2>&1 || true
  fi

  if [[ "$LEGACY_MIGRATION_PENDING" == "1" && "$LEGACY_UNIT_FOUND" == "1" ]]; then
    if [[ "$LEGACY_WAS_ENABLED" == "1" ]]; then
      systemctl --user enable "$LEGACY_UNIT" >/dev/null 2>&1 || true
    else
      systemctl --user disable "$LEGACY_UNIT" >/dev/null 2>&1 || true
    fi
    if [[ "$LEGACY_WAS_ACTIVE" == "1" ]]; then
      systemctl --user start "$LEGACY_UNIT" >/dev/null 2>&1 || true
    fi
  fi

  if [[ "$DB_MIGRATION_APPLIED" == "1" ]]; then
    echo "The auto-submission app was rolled back, but its successful additive database migration was retained." >&2
  fi
  if [[ "$LEGACY_MIGRATION_PENDING" == "1" ]]; then
    echo "New main deployment failed; the legacy service was restored best-effort." >&2
  elif [[ "$PREVIOUS_UNIT_ACTIVE" == "1" ]]; then
    echo "Deployment failed; the previous checkout, Compose environment, images, and service were restored best-effort." >&2
  else
    echo "Deployment failed; the attempted stack was stopped and prior deployment state was restored best-effort." >&2
  fi

  rm -rf -- "$ROLLBACK_DIR"
  exit "$status"
}
trap on_deploy_error ERR

if [[ ! -d "$DEPLOY_DIR/.git" ]]; then
  if [[ -e "$DEPLOY_DIR" ]]; then
    fail_deploy "Deployment path exists but is not a Git checkout."
  fi
  DEPLOYMENT_SWITCH_STARTED=1
  DEPLOY_CHECKOUT_CREATED=1
  git clone --no-tags --single-branch --branch "$DEPLOY_BRANCH" "$REPO_URL" "$DEPLOY_DIR"
  DEPLOY_CHECKOUT_CLONED=1
fi

git -C "$DEPLOY_DIR" fetch --force --no-tags origin "$DEPLOY_BRANCH"
git -C "$DEPLOY_DIR" cat-file -e "$DEPLOY_SHA^{commit}"
if [[ "$(git -C "$DEPLOY_DIR" rev-parse FETCH_HEAD)" != "$DEPLOY_SHA" ]]; then
  rm -rf -- "$ROLLBACK_DIR"
  echo "The requested deployment commit is no longer the expected branch tip." >&2
  exit 1
fi
DEPLOYMENT_SWITCH_STARTED=1
git -C "$DEPLOY_DIR" checkout --detach --force "$DEPLOY_SHA"
git -C "$DEPLOY_DIR" reset --hard "$DEPLOY_SHA"
git -C "$DEPLOY_DIR" clean -ffdx

cd "$DEPLOY_DIR"
if [[ ! -f "$COMPOSE_FILE" ]]; then
  fail_deploy "Deployment Compose file is missing from the verified commit."
fi
if [[ ! -f "$UNIT_SOURCE" ]]; then
  fail_deploy "Deployment systemd unit is missing from the verified commit."
fi

if [[ "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
  if [[ ! -f "$AUTO_ENV_FILE" ]]; then
    fail_deploy "Auto-submission host environment file is missing."
  fi
  auto_env_mode="$(stat -c '%a' "$AUTO_ENV_FILE")"
  if [[ "$auto_env_mode" != "600" && "$auto_env_mode" != "400" ]]; then
    fail_deploy "Auto-submission host environment file permissions must be 600 or 400."
  fi
  python3 deploy/scripts/validate-auto-environment.py "$AUTO_ENV_FILE"
  mkdir -p "$AUTO_UPDATES_DIR"
  chmod 0755 "$AUTO_UPDATES_DIR"
fi

compose_env_temp="$(mktemp "$STATE_DIR/compose.env.XXXXXX")"
cat > "$compose_env_temp" <<EOF
BUILD_SHA=$DEPLOY_SHA
TUFREPLAY_BUILD_SHA=$DEPLOY_SHA
TUFREPLAY_ENVIRONMENT=$DEPLOY_ENVIRONMENT
TUFREPLAY_BUILD_FLAVOR=$BUILD_FLAVOR
VITE_WEB_ADOFAI_EMBED_URL=https://web-adofai.impl1113.dev/embed/chart
AUTO_UPDATES_DIR=$AUTO_UPDATES_DIR
EOF
chmod 0600 "$compose_env_temp"
mv -f "$compose_env_temp" "$COMPOSE_ENV_FILE"
COMPOSE_ENV_CHANGED=1

if ! compose config --quiet >/dev/null 2>&1; then
  fail_deploy "Deployment Compose configuration is invalid; environment values were withheld."
fi

images=(
  "tuf-replay-web:$DEPLOY_ENVIRONMENT-$DEPLOY_SHA"
  "tuf-replay-gateway:$DEPLOY_ENVIRONMENT-$DEPLOY_SHA"
)
if [[ "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
  images+=("tuf-replay-server:auto-submission-$DEPLOY_SHA")
fi
for image in "${images[@]}"; do
  if ! docker image inspect "$image" >/dev/null 2>&1; then
    fail_deploy "A tested deployment image is missing on the home server."
  fi
done

if [[ "$DEPLOY_ENVIRONMENT" == "main" && ! -e "$MIGRATION_MARKER" ]]; then
  LEGACY_MIGRATION_PENDING=1
  if systemctl --user cat "$LEGACY_UNIT" >/dev/null 2>&1; then
    LEGACY_UNIT_FOUND=1
    if systemctl --user is-active --quiet "$LEGACY_UNIT"; then
      LEGACY_WAS_ACTIVE=1
    fi
    if systemctl --user is-enabled --quiet "$LEGACY_UNIT"; then
      LEGACY_WAS_ENABLED=1
    fi
    STACK_TOUCHED=1
    systemctl --user stop "$LEGACY_UNIT"
    if [[ "$LEGACY_WAS_ENABLED" == "1" ]]; then
      systemctl --user disable "$LEGACY_UNIT"
    fi
  fi
fi

if [[ "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
  STACK_TOUCHED=1
  compose up -d --wait postgres-production redis-ingest-production
  compose stop tuf-replay-server >/dev/null 2>&1 || true
  if ! compose run --rm --no-deps tuf-replay-server db migrate >/dev/null 2>&1; then
    fail_deploy "Auto-submission database migration failed; migration output was withheld."
  fi
  DB_MIGRATION_APPLIED=1
fi

STACK_TOUCHED=1
compose up -d --no-build --wait --remove-orphans

verify_local_deployment() {
  local response_file
  response_file="$(mktemp)"

  local ready=0
  for attempt in {1..30}; do
    if curl --fail --silent --max-time 5 "http://127.0.0.1:$PORT/deployment.json" -o "$response_file"; then
      if python3 - "$response_file" "$DEPLOY_ENVIRONMENT" "$BUILD_FLAVOR" "$DEPLOY_SHA" <<'PY'
import json
import sys

path, expected_environment, expected_flavor, expected_sha = sys.argv[1:]
with open(path, encoding="utf-8") as stream:
    info = json.load(stream)
if (
    info.get("environment") != expected_environment
    or info.get("buildFlavor") != expected_flavor
    or info.get("gitSHA") != expected_sha
):
    raise SystemExit("Local deployment metadata does not match the verified commit.")
PY
      then
        ready=1
        break
      fi
    fi
    sleep 2
  done

  if [[ "$ready" != "1" ]]; then
    echo "Local web deployment metadata did not become ready." >&2
    compose ps
    rm -f "$response_file"
    return 1
  fi

  if [[ "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
    curl --fail --silent --show-error --max-time 5 "http://127.0.0.1:$PORT/_readiness" >/dev/null
  fi
  rm -f "$response_file"
}

verify_public_deployment() {
  local response_file
  response_file="$(mktemp)"
  local ready=0
  for attempt in {1..30}; do
    if curl --fail --silent --max-time 10 "https://$DOMAIN/deployment.json" -o "$response_file"; then
      if python3 - "$response_file" "$DEPLOY_ENVIRONMENT" "$BUILD_FLAVOR" "$DEPLOY_SHA" <<'PY'
import json
import sys

path, expected_environment, expected_flavor, expected_sha = sys.argv[1:]
with open(path, encoding="utf-8") as stream:
    info = json.load(stream)
if (
    info.get("environment") != expected_environment
    or info.get("buildFlavor") != expected_flavor
    or info.get("gitSHA") != expected_sha
):
    raise SystemExit("Public deployment metadata does not match the verified commit.")
PY
      then
        ready=1
        break
      fi
    fi
    sleep 2
  done
  rm -f "$response_file"
  if [[ "$ready" != "1" ]]; then
    echo "Public deployment metadata did not become ready at https://$DOMAIN." >&2
    return 1
  fi
}

verify_local_deployment

mkdir -p "$USER_SYSTEMD_DIR"
UNIT_CHANGED=1
install -m 0644 "$UNIT_SOURCE" "$UNIT_TARGET"
systemctl --user daemon-reload
systemctl --user enable "$SERVICE_UNIT" >/dev/null
systemctl --user start "$SERVICE_UNIT"

if [[ "$DEPLOY_ENVIRONMENT" == "dev" || "$DEPLOY_ENVIRONMENT" == "auto-submission" ]]; then
  sudo -n /usr/local/sbin/tuf-replay-provision-routing
fi

verify_public_deployment

if [[ "$LEGACY_MIGRATION_PENDING" == "1" ]]; then
  mkdir -p "$(dirname "$MIGRATION_MARKER")"
  : > "$MIGRATION_MARKER"
  chmod 0600 "$MIGRATION_MARKER"
  LEGACY_MIGRATION_PENDING=0
fi

trap - ERR
rm -rf -- "$ROLLBACK_DIR"
echo "Deployed $DEPLOY_ENVIRONMENT at $DEPLOY_SHA to 127.0.0.1:$PORT ($DOMAIN)."
compose ps
