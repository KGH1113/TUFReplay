#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_dir "$TUFREPLAY_MAC_HELPER_APP"
require_file "$TUFREPLAY_MAC_HELPER_APP/Contents/Info.plist"
require_executable "$TUFREPLAY_MAC_HELPER_EXECUTABLE"
