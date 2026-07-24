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
package_bytes="$(wc -c < "$TUFREPLAY_PACKAGE_ZIP_PATH" | tr -d '[:space:]')"
package_sha256="$(shasum -a 256 "$TUFREPLAY_PACKAGE_ZIP_PATH" | awk '{print $1}')"

rm -f "$TUFREPLAY_PROJECT_ROOT/build/TUFReplay.version" \
  "$TUFREPLAY_PROJECT_ROOT/build/TUFReplay.zip.sha256"
mkdir -p "$(dirname "$TUFREPLAY_UPDATE_MANIFEST_PATH")"
printf '{\n  "schemaVersion": 1,\n  "version": "%s",\n  "packageAsset": "TUFReplay.zip",\n  "packageBytes": %s,\n  "packageSha256": "%s",\n  "runtimePath": "TUFReplay/Runtime/versions/%s"\n}\n' \
  "$version" "$package_bytes" "$package_sha256" "$version" > "$TUFREPLAY_UPDATE_MANIFEST_PATH"

printf 'Update manifest: %s\n' "$TUFREPLAY_UPDATE_MANIFEST_PATH"
