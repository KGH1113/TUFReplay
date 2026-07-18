#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/manifests.sh
source "$TASK_DIR/../../lib/manifests.sh"
# shellcheck source=../../lib/dependencies.sh
source "$TASK_DIR/../../lib/dependencies.sh"

require_command zip
require_command shasum
require_file "$DOTNET_EXE"
require_dir "$ADOFAI_MANAGED"
require_file "$UNITY_MOD_MANAGER_DLL"
require_file "$HARMONY_DLL"
require_file "$ADOFAI_IPC_DLL"
require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"
require_file "$ADOFAI_IPC_INFO_JSON"
require_file "$ADOFAI_IPC_BOOTSTRAP_LOCK"

for bundle in "${TUFREPLAY_UI_BUNDLES[@]}"; do
  require_file "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Assets/$bundle"
done
require_file "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Assets/calibration/level.adofai"
require_file "$TUFREPLAY_PROJECT_ROOT/TUFReplay/Assets/calibration/calibration_old.ogg"

sourcegear_sqlite3_version >/dev/null
