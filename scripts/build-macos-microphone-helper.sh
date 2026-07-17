#!/usr/bin/env bash
set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
XCODE_PROJECT="$PROJECT_ROOT/TUFReplay.MicrophoneCapture.Mac/TUFReplayMicrophoneCapture.xcodeproj"
SCHEME="TUFReplayMicrophoneCapture"
OUTPUT="${TUFREPLAY_MAC_HELPER_BUILD_DIR:-$PROJECT_ROOT/build/macos-helper}"
DERIVED_DATA="$OUTPUT/DerivedData"
BUILT_APP="$DERIVED_DATA/Build/Products/Release/TUFReplayMicrophoneCapture.app"
APP="$OUTPUT/TUFReplayMicrophoneCapture.app"
EXECUTABLE="$APP/Contents/MacOS/TUFReplayMicrophoneCapture"

if [ "$(uname -s)" != "Darwin" ]; then
  echo "The macOS microphone helper can only be built on macOS." >&2
  exit 1
fi

if [ ! -d "$XCODE_PROJECT" ]; then
  echo "Missing Xcode project: $XCODE_PROJECT" >&2
  exit 1
fi

mkdir -p "$OUTPUT"

xcodebuild \
  -quiet \
  -project "$XCODE_PROJECT" \
  -scheme "$SCHEME" \
  -configuration Release \
  -derivedDataPath "$DERIVED_DATA" \
  ARCHS="arm64 x86_64" \
  ONLY_ACTIVE_ARCH=NO \
  CODE_SIGNING_ALLOWED=NO \
  CODE_SIGNING_REQUIRED=NO \
  MACOSX_DEPLOYMENT_TARGET=12.0 \
  clean build

if [ ! -d "$BUILT_APP" ]; then
  echo "Xcode did not produce the helper app: $BUILT_APP" >&2
  exit 1
fi

rm -rf "$APP"
ditto "$BUILT_APP" "$APP"

if [ ! -x "$EXECUTABLE" ]; then
  echo "Missing helper executable: $EXECUTABLE" >&2
  exit 1
fi

lipo "$EXECUTABLE" -verify_arch arm64 x86_64

BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Contents/Info.plist")"
if [ "$BUNDLE_ID" != "impl.tufreplay.microphone-capture" ]; then
  echo "Unexpected helper bundle identifier: $BUNDLE_ID" >&2
  exit 1
fi

"$EXECUTABLE" --self-test
codesign --force --sign - --timestamp=none "$APP"
codesign --verify --deep --strict "$APP"

echo "$APP"
