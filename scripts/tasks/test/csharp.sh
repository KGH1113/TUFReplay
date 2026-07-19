#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
# shellcheck source=../../lib/dependencies.sh
source "$TASK_DIR/../../lib/dependencies.sh"

sqlite_library="$(macos_test_sqlite_library)"
require_file "$sqlite_library"
require_file "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll"

TUFREPLAY_SQLITE_NATIVE_LIBRARY="$sqlite_library" \
DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
  "$DOTNET_EXE" run --project "$TUFREPLAY_PROJECT_ROOT/TUFReplay.Tests/TUFReplay.Tests.csproj" \
    -p:TUFReplayDll="$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll" \
    -p:AdofaiManaged="$ADOFAI_MANAGED"
