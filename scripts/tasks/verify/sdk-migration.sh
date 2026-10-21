#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
source "$TASK_DIR/../../lib/guards.sh"

baseline="$TUFREPLAY_PROJECT_ROOT/build/migration-baseline"
mkdir -p "$baseline"
require_command gh
gh release download v0.2.0-beta.4 --repo KGH1113/TUFReplay \
  --pattern TUFReplay.zip --dir "$baseline" --skip-existing
printf '%s  %s\n' '5728993cec95ba0adadd04804ffb6dd7bcbbdaf9fe5f96c64564bbfd3b2f70b3' "$baseline/TUFReplay.zip" | shasum -a 256 -c -
unzip -q -o "$baseline/TUFReplay.zip" -d "$baseline/extracted"
require_file "$TUFREPLAY_PACKAGE_ZIP_PATH"
require_file "$TUFREPLAY_UPDATE_MANIFEST_PATH"
export TUFREPLAY_LEGACY_ROOT="$baseline/extracted/TUFReplay"
export TUFREPLAY_MIGRATION_PACKAGE="$TUFREPLAY_PACKAGE_ZIP_PATH"
export TUFREPLAY_MIGRATION_MANIFEST="$TUFREPLAY_UPDATE_MANIFEST_PATH"
exec bash "$TASK_DIR/../test/csharp.sh"
