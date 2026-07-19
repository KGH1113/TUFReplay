#!/usr/bin/env bash

if [ "${TUFREPLAY_ARTIFACTS_LOADED:-0}" = "1" ]; then
  return 0
fi
TUFREPLAY_ARTIFACTS_LOADED=1

TUFREPLAY_ARTIFACTS_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=context.sh
source "$TUFREPLAY_ARTIFACTS_LIB_DIR/context.sh"
# shellcheck source=guards.sh
source "$TUFREPLAY_ARTIFACTS_LIB_DIR/guards.sh"
# shellcheck source=manifests.sh
source "$TUFREPLAY_ARTIFACTS_LIB_DIR/manifests.sh"
# shellcheck source=dependencies.sh
source "$TUFREPLAY_ARTIFACTS_LIB_DIR/dependencies.sh"

copy_core_payload() {
  local destination="$1"

  require_file "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json"
  require_file "$TUFREPLAY_PROJECT_ROOT/TUFReplay/AdofaiIpcBootstrap.json"
  require_file "$TUFREPLAY_PROJECT_ROOT/THIRD_PARTY_NOTICES.md"
  require_file "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll"
  require_file "$TUFREPLAY_BOOTSTRAP_BUILD_OUTPUT/TUFReplay.Bootstrap.dll"
  require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"

  mkdir -p "$destination"
  cp "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json" "$destination/"
  cp "$TUFREPLAY_PROJECT_ROOT/TUFReplay/AdofaiIpcBootstrap.json" "$destination/"
  cp "$TUFREPLAY_PROJECT_ROOT/THIRD_PARTY_NOTICES.md" "$destination/"
  cp "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll" "$destination/"
  cp "$TUFREPLAY_BOOTSTRAP_BUILD_OUTPUT/TUFReplay.Bootstrap.dll" "$destination/"
  cp "$ADOFAI_IPC_BOOTSTRAP_DLL" "$destination/"
}

copy_runtime_dependencies() {
  local destination="$1"
  local requirement="${2:-required}"
  local dll

  for dll in "${TUFREPLAY_RUNTIME_DLLS[@]}"; do
    if [ "$requirement" = "required" ]; then
      require_file "$TUFREPLAY_BUILD_OUTPUT/$dll"
    fi
    if [ -f "$TUFREPLAY_BUILD_OUTPUT/$dll" ]; then
      cp "$TUFREPLAY_BUILD_OUTPUT/$dll" "$destination/"
    fi
  done
}

copy_assets() {
  local destination="$1"
  local requirement="${2:-required}"
  local source="$TUFREPLAY_PROJECT_ROOT/TUFReplay/Assets"
  local target="$destination/Assets"

  if [ "$requirement" = "required" ]; then
    require_dir "$source"
  elif [ ! -d "$source" ]; then
    return 0
  fi

  if [ -e "$target" ]; then
    safe_remove_tree "$target" "$destination"
  fi
  cp -R "$source" "$target"
}

copy_mac_helper() {
  local destination="$1"
  local requirement="${2:-required}"
  local helpers_root="$destination/Helpers"
  local target="$helpers_root/mac"

  if [ "$requirement" = "required" ]; then
    require_dir "$TUFREPLAY_MAC_HELPER_APP"
  elif [ ! -d "$TUFREPLAY_MAC_HELPER_APP" ]; then
    return 0
  fi

  mkdir -p "$helpers_root"
  if [ -e "$target" ]; then
    safe_remove_tree "$target" "$helpers_root"
  fi
  mkdir -p "$target"
  cp -R "$TUFREPLAY_MAC_HELPER_APP" "$target/"
}

copy_debug_symbols() {
  local destination="$1"

  if [ -f "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.pdb" ]; then
    cp "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.pdb" "$destination/"
  fi
}

copy_sqlite_override() {
  local destination="$1"
  local target

  if [ -z "${SQLITE_NATIVE_LIBRARY:-}" ]; then
    return 0
  fi

  require_file "$SQLITE_NATIVE_LIBRARY"
  target="$destination/$(basename "$SQLITE_NATIVE_LIBRARY")"
  cp "$SQLITE_NATIVE_LIBRARY" "$target"
  chmod +x "$target" 2>/dev/null || true
  if command -v xattr >/dev/null 2>&1; then
    xattr -d com.apple.quarantine "$target" 2>/dev/null || true
    xattr -d com.apple.provenance "$target" 2>/dev/null || true
  fi
}

copy_windows_sqlite() {
  local destination="$1"
  local library

  library="$(windows_sqlite_library)"
  require_file "$library"
  cp "$library" "$destination/e_sqlite3.dll"
}
