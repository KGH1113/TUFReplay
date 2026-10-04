#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

bench_mono="${TUFREPLAY_CAMERA_COPY_BENCH_MONO:-}"
if [ -z "$bench_mono" ]; then
  for candidate in /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Resources/Scripting/MonoBleedingEdge/bin/mono; do
    if [ -x "$candidate" ]; then
      bench_mono="$candidate"
    fi
  done
fi
if [ -z "$bench_mono" ]; then
  bench_mono="$(command -v mono || true)"
fi
[ -n "$bench_mono" ] || fail "Set TUFREPLAY_CAMERA_COPY_BENCH_MONO to a standalone Mono CLI."
require_executable "$bench_mono"
bench_compiler="${bench_mono%/*}/mcs"
require_executable "$bench_compiler"
require_command mktemp
bench_source="$TUFREPLAY_PROJECT_ROOT/scripts/benchmarks/CameraPreviewCopyBench.cs"
require_file "$bench_source"
bench_temp_root="${TMPDIR:-/tmp}"
bench_temp_root="${bench_temp_root%/}"
bench_dir="$(mktemp -d "$bench_temp_root/tufreplay-camera-copy-bench.XXXXXX")"
trap 'safe_remove_tree "$bench_dir" "$bench_temp_root"' EXIT

bench_version="$("$bench_mono" --version)"
printf '%s\n' "${bench_version%%$'\n'*}"
printf 'Standalone copy benchmark; these results are not game FPS or Game Mode measurements.\n'
"$bench_compiler" -unsafe -optimize+ -out:"$bench_dir/CameraPreviewCopyBench.exe" "$bench_source"
"$bench_mono" "$bench_dir/CameraPreviewCopyBench.exe" "$bench_dir" "${1:-100}"
