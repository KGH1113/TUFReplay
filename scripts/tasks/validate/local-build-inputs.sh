#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/build-metadata.sh
source "$TASK_DIR/../../lib/build-metadata.sh"

require_file "$DOTNET_EXE"
require_command python3
tufreplay_resolve_build_version "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Info.json" >/dev/null
require_dir "$ADOFAI_MANAGED"
require_file "$UNITY_MOD_MANAGER_DLL"
require_file "$ADOFAI_IPC_DLL"
require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"
require_file "$ADOFAI_IPC_DEPENDENCY_SHIM_DLL"
require_file "$ADOFAI_IPC_MIGRATION_DLL"
