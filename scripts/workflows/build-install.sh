#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$(cd "$WORKFLOW_DIR/.." && pwd)"
TASKS_DIR="$SCRIPTS_DIR/tasks"
# shellcheck source=../lib/context.sh
source "$SCRIPTS_DIR/lib/context.sh"
# shellcheck source=../lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

run_task "Validate local build inputs" "$TASKS_DIR/validate/local-build-inputs.sh"

if is_macos; then
  run_task "Build macOS microphone helper" "$TASKS_DIR/build/macos-microphone-helper.sh"
else
  log_skip "Build macOS microphone helper (macOS only)"
fi

run_task "Build bootstrap (Debug)" "$TASKS_DIR/build/bootstrap.sh" Debug
run_task "Build mod (Debug)" "$TASKS_DIR/build/mod.sh" Debug

if is_macos; then
  run_task "Run C# tests" "$TASKS_DIR/test/csharp.sh"
else
  log_skip "Run C# tests (macOS native SQLite test setup only)"
fi

run_task "Install mod" "$TASKS_DIR/install/mod.sh"
