#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/artifacts.sh
source "$TASK_DIR/../../lib/artifacts.sh"

assert_non_root_path "$TUFREPLAY_PACKAGE_BUILD_ROOT"
assert_non_root_path "$TUFREPLAY_PACKAGE_STAGE"
mkdir -p "$TUFREPLAY_PACKAGE_BUILD_ROOT"
if [ -e "$TUFREPLAY_PACKAGE_STAGE" ]; then
  safe_remove_tree "$TUFREPLAY_PACKAGE_STAGE" "$TUFREPLAY_PACKAGE_BUILD_ROOT"
fi
mkdir -p "$TUFREPLAY_PACKAGE_STAGE"

version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json" | head -n 1)"
[ -n "$version" ] || fail "TUFReplay version is missing from Info.json."
runtime="$TUFREPLAY_PACKAGE_STAGE/Runtime/versions/$version"

copy_launcher_payload "$TUFREPLAY_PACKAGE_STAGE"
copy_runtime_core "$runtime"
copy_windows_sqlite "$runtime"
copy_assets "$runtime" required
copy_mac_helper "$runtime" required
copy_runtime_dependencies "$runtime" required

mkdir -p "$TUFREPLAY_PACKAGE_STAGE/Runtime"
printf '{\n  "SchemaVersion": 1,\n  "Current": "%s",\n  "Previous": null,\n  "Trial": null\n}\n' \
  "$version" > "$TUFREPLAY_PACKAGE_STAGE/Runtime/state.json"
