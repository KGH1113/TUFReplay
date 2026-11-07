#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

test_mono="${TUFREPLAY_TEST_MONO:-}"
if [ -z "$test_mono" ]; then
  for candidate in /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge/bin/mono; do
    if [ -x "$candidate" ]; then test_mono="$candidate"; fi
  done
fi
if [ -z "$test_mono" ]; then test_mono="$(command -v mono || true)"; fi
[ -n "$test_mono" ] || fail "Set TUFREPLAY_TEST_MONO to a standalone Mono CLI."
require_executable "$test_mono"
test_compiler="${test_mono%/*}/mcs"
require_executable "$test_compiler"
require_file "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll"
test_version="$("$test_mono" --version)"
printf '%s\n' "${test_version%%$'\n'*}"
test_temp_root="${TMPDIR:-/tmp}"
test_temp_root="${test_temp_root%/}"
test_dir="$(mktemp -d "$test_temp_root/tufreplay-ffmpeg-mono.XXXXXX")"
trap 'safe_remove_tree "$test_dir" "$test_temp_root"' EXIT

"$test_compiler" -out:"$test_dir/ManagedFfmpegMono.exe" "$TUFREPLAY_PROJECT_ROOT/scripts/tests/ManagedFfmpegMono.cs"
MONO_PATH="$TUFREPLAY_BUILD_OUTPUT:$ADOFAI_MANAGED" \
  "$test_mono" "$test_dir/ManagedFfmpegMono.exe" "$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll" "$test_dir/game" "$test_mono"
