#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
bash "$TASK_DIR/adofai-ipc.sh"
bun run --cwd "$TUFREPLAY_PROJECT_ROOT/web" ipc:check
fixture_dir="$TUFREPLAY_PROJECT_ROOT/build/tests/ipc"
"$DOTNET_EXE" build "$TUFREPLAY_PROJECT_ROOT/TUFReplay.IpcTests/TUFReplay.IpcTests.csproj" \
  --configuration Release --output "$fixture_dir" --nologo
TUFREPLAY_IPC_FIXTURE="$fixture_dir/TUFReplay.IpcTests.dll" \
  bun "$TUFREPLAY_PROJECT_ROOT/web/scripts/test-ipc-runtime.ts"
