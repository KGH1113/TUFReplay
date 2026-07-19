#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command zip
require_dir "$TUFREPLAY_PACKAGE_STAGE"

rm -f "$TUFREPLAY_PACKAGE_ZIP_PATH"
mkdir -p "$(dirname "$TUFREPLAY_PACKAGE_ZIP_PATH")"
(
  cd "$TUFREPLAY_PACKAGE_BUILD_ROOT"
  zip -r "$TUFREPLAY_PACKAGE_ZIP_PATH" TUFReplay \
    -x 'TUFReplay/Data/*' \
    -x 'TUFReplay/*.sqlite' \
    -x 'TUFReplay/*.sqlite3' \
    -x 'TUFReplay/*.db' \
    -x 'TUFReplay/*.db-shm' \
    -x 'TUFReplay/*.db-wal' \
    -x 'TUFReplay/*.log'
)

printf 'Packaged to %s\n' "$TUFREPLAY_PACKAGE_ZIP_PATH"
