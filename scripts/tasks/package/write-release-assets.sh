#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command shasum
require_file "$TUFREPLAY_PACKAGE_ZIP_PATH"

version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json" | head -n 1)"
[ -n "$version" ] || fail "TUFReplay version is missing from Info.json."

mkdir -p "$(dirname "$TUFREPLAY_VERSION_ASSET_PATH")"
mkdir -p "$(dirname "$TUFREPLAY_CHECKSUM_ASSET_PATH")"
printf '%s\n' "$version" > "$TUFREPLAY_VERSION_ASSET_PATH"
shasum -a 256 "$TUFREPLAY_PACKAGE_ZIP_PATH" > "$TUFREPLAY_CHECKSUM_ASSET_PATH"

printf 'Version asset: %s\n' "$TUFREPLAY_VERSION_ASSET_PATH"
printf 'Checksum asset: %s\n' "$TUFREPLAY_CHECKSUM_ASSET_PATH"
