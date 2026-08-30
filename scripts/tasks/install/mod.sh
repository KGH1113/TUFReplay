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

for obsolete_dir in assembly_cache dependency .tufreplay-update Runtime; do
  if [ -e "$TUFREPLAY_INSTALL_PATH/$obsolete_dir" ]; then
    safe_remove_tree "$TUFREPLAY_INSTALL_PATH/$obsolete_dir" "$TUFREPLAY_INSTALL_PATH"
  fi
done
rm -f "$TUFREPLAY_INSTALL_PATH/JAModInfo.json" "$TUFREPLAY_INSTALL_PATH/JAMod.Bootstrap.dll"
rm -f "$TUFREPLAY_INSTALL_PATH"/JAMod.Bootstrap.dll.*.cache
rm -f "$TUFREPLAY_INSTALL_PATH/TUFReplay.dll" "$TUFREPLAY_INSTALL_PATH/TUFReplay.pdb"
for runtime_dll in "${TUFREPLAY_RUNTIME_DLLS[@]}"; do
  rm -f "$TUFREPLAY_INSTALL_PATH/$runtime_dll"
done
rm -f "$TUFREPLAY_INSTALL_PATH/e_sqlite3.dll" \
  "$TUFREPLAY_INSTALL_PATH/libe_sqlite3.dylib" \
  "$TUFREPLAY_INSTALL_PATH/libe_sqlite3.so" \
  "$TUFREPLAY_INSTALL_PATH/libTUFReplayInput.dylib"
for legacy_payload_dir in Assets Helpers; do
  if [ -e "$TUFREPLAY_INSTALL_PATH/$legacy_payload_dir" ]; then
    safe_remove_tree "$TUFREPLAY_INSTALL_PATH/$legacy_payload_dir" "$TUFREPLAY_INSTALL_PATH"
  fi
done
for platform in mac win linux; do
  rm -f "$TUFREPLAY_INSTALL_PATH/Assets/$platform/tufreplay_ui.bundle"
done

version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json" | head -n 1)"
[ -n "$version" ] || fail "TUFReplay version is missing from Info.json."
runtime="$TUFREPLAY_INSTALL_PATH/Runtime/versions/$version"

copy_launcher_payload "$TUFREPLAY_INSTALL_PATH"
copy_runtime_core "$runtime"
copy_assets "$runtime" optional
if is_macos; then
  copy_mac_helper "$runtime" required
  copy_mac_native_input "$runtime" required
fi
copy_runtime_dependencies "$runtime" optional
copy_sqlite_override "$runtime"
copy_debug_symbols "$runtime"

mkdir -p "$TUFREPLAY_INSTALL_PATH/Runtime"
printf '{\n  "SchemaVersion": 1,\n  "Current": "%s",\n  "Previous": null,\n  "Trial": null\n}\n' \
  "$version" > "$TUFREPLAY_INSTALL_PATH/Runtime/state.json"

printf 'Installed to %s\n' "$TUFREPLAY_INSTALL_PATH"
