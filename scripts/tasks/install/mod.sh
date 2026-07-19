#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/artifacts.sh
source "$TASK_DIR/../../lib/artifacts.sh"

assert_non_root_path "$TUFREPLAY_INSTALL_PATH"
mkdir -p "$TUFREPLAY_INSTALL_PATH"

for obsolete_dir in assembly_cache dependency .tufreplay-update; do
  if [ -e "$TUFREPLAY_INSTALL_PATH/$obsolete_dir" ]; then
    safe_remove_tree "$TUFREPLAY_INSTALL_PATH/$obsolete_dir" "$TUFREPLAY_INSTALL_PATH"
  fi
done
rm -f "$TUFREPLAY_INSTALL_PATH/JAModInfo.json" "$TUFREPLAY_INSTALL_PATH/JAMod.Bootstrap.dll"
rm -f "$TUFREPLAY_INSTALL_PATH"/JAMod.Bootstrap.dll.*.cache

copy_core_payload "$TUFREPLAY_INSTALL_PATH"
copy_assets "$TUFREPLAY_INSTALL_PATH" optional
if is_macos; then
  copy_mac_helper "$TUFREPLAY_INSTALL_PATH" required
fi
copy_runtime_dependencies "$TUFREPLAY_INSTALL_PATH" optional
copy_sqlite_override "$TUFREPLAY_INSTALL_PATH"
copy_debug_symbols "$TUFREPLAY_INSTALL_PATH"

printf 'Installed to %s\n' "$TUFREPLAY_INSTALL_PATH"
