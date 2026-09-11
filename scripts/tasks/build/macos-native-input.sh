#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

is_macos || fail "The macOS native input shim can only be built on macOS."
require_command xcrun
require_command lipo
require_command codesign
require_command nm

source_root="$TUFREPLAY_PROJECT_ROOT/TUFReplay.NativeInput.Mac"
include_dir="$source_root/include"
implementation="$source_root/src/tufreplay_input.mm"
core_test="$source_root/tests/input_core_tests.cpp"
mkdir -p "$TUFREPLAY_MAC_INPUT_BUILD_OUTPUT"

xcrun clang++ \
  -std=c++17 \
  -fobjc-arc \
  -O2 \
  -Wall \
  -Wextra \
  -Werror \
  -arch arm64 \
  -arch x86_64 \
  -mmacosx-version-min=12.0 \
  -dynamiclib \
  -install_name @rpath/libTUFReplayInput.dylib \
  -I"$include_dir" \
  -I"$source_root/src" \
  "$implementation" \
  -framework IOKit \
  -framework CoreFoundation \
  -framework CoreGraphics \
  -o "$TUFREPLAY_MAC_INPUT_LIBRARY"

xcrun clang++ \
  -std=c++17 \
  -O2 \
  -Wall \
  -Wextra \
  -Werror \
  -mmacosx-version-min=12.0 \
  -I"$include_dir" \
  -I"$source_root/src" \
  "$core_test" \
  -o "$TUFREPLAY_MAC_INPUT_TEST"

"$TUFREPLAY_MAC_INPUT_TEST"
lipo "$TUFREPLAY_MAC_INPUT_LIBRARY" -verify_arch arm64 x86_64
codesign --force --sign - --timestamp=none "$TUFREPLAY_MAC_INPUT_LIBRARY"
codesign --verify --strict "$TUFREPLAY_MAC_INPUT_LIBRARY"
printf '%s\n' "$TUFREPLAY_MAC_INPUT_LIBRARY"
