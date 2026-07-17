#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

is_macos || fail "The macOS microphone helper can only be built on macOS."

require_command xcodebuild
require_command ditto
require_command lipo
require_command codesign
require_dir "$TUFREPLAY_MAC_HELPER_XCODE_PROJECT"
assert_non_root_path "$TUFREPLAY_MAC_HELPER_BUILD_OUTPUT"

mkdir -p "$TUFREPLAY_MAC_HELPER_BUILD_OUTPUT"

xcodebuild \
  -quiet \
  -project "$TUFREPLAY_MAC_HELPER_XCODE_PROJECT" \
  -scheme TUFReplayMicrophoneCapture \
  -configuration Release \
  -derivedDataPath "$TUFREPLAY_MAC_HELPER_DERIVED_DATA" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  CODE_SIGNING_ALLOWED=NO \
  CODE_SIGNING_REQUIRED=NO \
  MACOSX_DEPLOYMENT_TARGET=12.0 \
  clean build

require_dir "$TUFREPLAY_MAC_HELPER_BUILT_APP"

if [ -e "$TUFREPLAY_MAC_HELPER_APP" ]; then
  safe_remove_tree "$TUFREPLAY_MAC_HELPER_APP" "$TUFREPLAY_MAC_HELPER_BUILD_OUTPUT"
fi
ditto "$TUFREPLAY_MAC_HELPER_BUILT_APP" "$TUFREPLAY_MAC_HELPER_APP"

require_executable "$TUFREPLAY_MAC_HELPER_EXECUTABLE"
lipo "$TUFREPLAY_MAC_HELPER_EXECUTABLE" -verify_arch arm64 x86_64

bundle_id="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$TUFREPLAY_MAC_HELPER_APP/Contents/Info.plist")"
if [ "$bundle_id" != "impl.tufreplay.microphone-capture" ]; then
  fail "Unexpected helper bundle identifier: $bundle_id"
fi

self_test_profile="$TUFREPLAY_MAC_HELPER_BUILD_OUTPUT/self-test.profraw"
rm -f "$self_test_profile"
LLVM_PROFILE_FILE="$self_test_profile" "$TUFREPLAY_MAC_HELPER_EXECUTABLE" --self-test
rm -f "$self_test_profile"
codesign --force --sign - --timestamp=none "$TUFREPLAY_MAC_HELPER_APP"
codesign --verify --deep --strict "$TUFREPLAY_MAC_HELPER_APP"

printf '%s\n' "$TUFREPLAY_MAC_HELPER_APP"
