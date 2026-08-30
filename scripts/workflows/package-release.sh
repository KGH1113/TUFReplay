#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

run_task "Validate release package inputs" "$TASKS_DIR/validate/release-package-inputs.sh"

if is_macos; then
  run_task "Build macOS microphone helper" "$TASKS_DIR/build/macos-microphone-helper.sh"
  run_task "Build macOS native input shim" "$TASKS_DIR/build/macos-native-input.sh"
else
  log_skip "Build macOS microphone helper (using existing artifact)"
  log_skip "Build macOS native input shim (using existing artifact)"
fi

run_task "Validate macOS helper artifact" "$TASKS_DIR/validate/macos-helper-artifact.sh"
run_task "Validate macOS native input artifact" "$TASKS_DIR/validate/macos-native-input-artifact.sh"
run_task "Verify AdofaiIpc dependency" "$TASKS_DIR/verify/adofai-ipc.sh"
run_task "Build bootstrap (Release)" "$TASKS_DIR/build/bootstrap.sh" Release
run_task "Build update engine (Release)" "$TASKS_DIR/build/update-engine.sh" Release
run_task "Build mod (Release)" "$TASKS_DIR/build/mod.sh" Release
run_task "Validate Unity/Mono compatibility" \
  "$TASKS_DIR/validate/unity-mono-compatibility.sh" \
  "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll" \
  "$TUFREPLAY_BOOTSTRAP_BUILD_OUTPUT/TUFReplay.Bootstrap.dll" \
  "$TUFREPLAY_UPDATE_ENGINE_BUILD_OUTPUT/TUFReplay.UpdateEngine.dll"
run_task "Stage package" "$TASKS_DIR/package/stage.sh"
run_task "Validate staged Unity/Mono compatibility" \
  "$TASKS_DIR/validate/unity-mono-compatibility.sh" \
  "$TUFREPLAY_PACKAGE_STAGE"
run_task "Create package archive" "$TASKS_DIR/package/archive.sh"
run_task "Validate final package Unity/Mono compatibility" \
  "$TASKS_DIR/validate/unity-mono-compatibility.sh" \
  "$TUFREPLAY_PACKAGE_ZIP_PATH"
run_task "Write release metadata" "$TASKS_DIR/package/write-release-assets.sh"
