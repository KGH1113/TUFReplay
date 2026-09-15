#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/build-metadata.sh
source "$TASK_DIR/../../lib/build-metadata.sh"

require_command shasum
require_file "$TUFREPLAY_PACKAGE_ZIP_PATH"

version="$(tufreplay_resolve_build_version "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json")"
IFS=$'\t' read -r staged_version staged_flavor <<< "$(tufreplay_read_build_info "$TUFREPLAY_PACKAGE_STAGE/Info.json")"
IFS=$'\t' read -r runtime_version runtime_flavor <<< "$(tufreplay_read_build_info "$TUFREPLAY_PACKAGE_STAGE/Runtime/versions/$version/Info.json")"
if [ "$version" != "$staged_version" ] || [ "$version" != "$runtime_version" ]; then
  fail "The staged launcher, runtime, and requested package versions do not match."
fi
if [ "$TUFREPLAY_BUILD_FLAVOR" != "$staged_flavor" ] || [ "$staged_flavor" != "$runtime_flavor" ]; then
  fail "The staged launcher, runtime, and requested build flavors do not match."
fi
package_bytes="$(wc -c < "$TUFREPLAY_PACKAGE_ZIP_PATH" | tr -d '[:space:]')"
package_sha256="$(shasum -a 256 "$TUFREPLAY_PACKAGE_ZIP_PATH" | awk '{print $1}')"

rm -f "$TUFREPLAY_PROJECT_ROOT/build/TUFReplay.version" \
  "$TUFREPLAY_PROJECT_ROOT/build/TUFReplay.zip.sha256"
mkdir -p "$(dirname "$TUFREPLAY_UPDATE_MANIFEST_PATH")"
if [ "$TUFREPLAY_BUILD_FLAVOR" = "auto-submission" ]; then
  package_url="versions/$version/TUFReplay.zip"
  printf '{\n  "schemaVersion": 1,\n  "version": "%s",\n  "buildFlavor": "%s",\n  "packageAsset": "TUFReplay.zip",\n  "packageUrl": "%s",\n  "packageBytes": %s,\n  "packageSha256": "%s",\n  "runtimePath": "TUFReplay/Runtime/versions/%s"\n}\n' \
    "$version" "$TUFREPLAY_BUILD_FLAVOR" "$package_url" "$package_bytes" "$package_sha256" "$version" \
    > "$TUFREPLAY_UPDATE_MANIFEST_PATH"
else
  printf '{\n  "schemaVersion": 1,\n  "version": "%s",\n  "buildFlavor": "%s",\n  "packageAsset": "TUFReplay.zip",\n  "packageBytes": %s,\n  "packageSha256": "%s",\n  "runtimePath": "TUFReplay/Runtime/versions/%s"\n}\n' \
    "$version" "$TUFREPLAY_BUILD_FLAVOR" "$package_bytes" "$package_sha256" "$version" \
    > "$TUFREPLAY_UPDATE_MANIFEST_PATH"
fi

printf 'Update manifest: %s\n' "$TUFREPLAY_UPDATE_MANIFEST_PATH"
