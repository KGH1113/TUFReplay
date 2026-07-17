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

copy_core_payload "$TUFREPLAY_PACKAGE_STAGE"
copy_windows_sqlite "$TUFREPLAY_PACKAGE_STAGE"
copy_assets "$TUFREPLAY_PACKAGE_STAGE" required
copy_mac_helper "$TUFREPLAY_PACKAGE_STAGE" required
copy_runtime_dependencies "$TUFREPLAY_PACKAGE_STAGE" required
