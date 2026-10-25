#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

# Cross-compiles TUFReplayNative.dll (low-level keyboard hook and audio endpoint
# queries) for x86_64 Windows with zig, on macOS or Linux.
require_command zig

source_root="$TUFREPLAY_PROJECT_ROOT/TUFReplay.Native.Windows"
include_dir="$source_root/include"
mkdir -p "$TUFREPLAY_WIN_NATIVE_BUILD_OUTPUT"

common_flags=(-std=c++17 -O2 -Wall -Wextra -Werror -I"$include_dir" -I"$source_root/src")

# The event ring is platform independent; run its tests on the build host.
zig c++ "${common_flags[@]}" "$source_root/tests/event_ring_tests.cpp" \
  -o "$TUFREPLAY_WIN_NATIVE_BUILD_OUTPUT/event-ring-tests"
"$TUFREPLAY_WIN_NATIVE_BUILD_OUTPUT/event-ring-tests"

zig c++ -target x86_64-windows-gnu "${common_flags[@]}" -fno-exceptions -fno-rtti -shared \
  "$source_root/src/keyboard_hook.cpp" \
  "$source_root/src/audio_endpoints.cpp" \
  -luser32 -lole32 \
  -o "$TUFREPLAY_WIN_NATIVE_LIBRARY"

# Desktop smoke test for a Windows machine; see TUFReplay.Native.Windows/README.md.
zig c++ -target x86_64-windows-gnu "${common_flags[@]}" -municode \
  "$source_root/tests/win_native_smoke.cpp" \
  -luser32 \
  -o "$TUFREPLAY_WIN_NATIVE_SMOKE"

rm -f "$TUFREPLAY_WIN_NATIVE_BUILD_OUTPUT"/*.pdb "$TUFREPLAY_WIN_NATIVE_BUILD_OUTPUT"/*.lib
printf '%s\n' "$TUFREPLAY_WIN_NATIVE_LIBRARY"
