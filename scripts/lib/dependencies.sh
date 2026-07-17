#!/usr/bin/env bash

if [ "${TUFREPLAY_DEPENDENCIES_LOADED:-0}" = "1" ]; then
  return 0
fi
TUFREPLAY_DEPENDENCIES_LOADED=1

TUFREPLAY_DEPENDENCIES_LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=context.sh
source "$TUFREPLAY_DEPENDENCIES_LIB_DIR/context.sh"
# shellcheck source=guards.sh
source "$TUFREPLAY_DEPENDENCIES_LIB_DIR/guards.sh"

sourcegear_sqlite3_version() {
  local version="${SOURCEGEAR_SQLITE3_VERSION:-}"

  if [ -z "$version" ]; then
    version="$(sed -n 's/.*PackageReference Include="SourceGear\.sqlite3" Version="\([^"]*\)".*/\1/p' "$TUFREPLAY_PROJECT_ROOT/TUFReplay/TUFReplay.csproj" | head -n 1)"
  fi

  [ -n "$version" ] || fail "Missing SourceGear.sqlite3 PackageReference version in TUFReplay.csproj"
  printf '%s\n' "$version"
}

macos_test_sqlite_library() {
  local architecture
  local version

  architecture="$(uname -m)"
  if [ "$architecture" = "x86_64" ]; then
    architecture="x64"
  fi
  version="$(sourcegear_sqlite3_version)"

  printf '%s/sourcegear.sqlite3/%s/runtimes/osx-%s/native/libe_sqlite3.dylib\n' \
    "$TUFREPLAY_NUGET_PACKAGES_DIR" "$version" "$architecture"
}

windows_sqlite_library() {
  local version
  version="$(sourcegear_sqlite3_version)"
  printf '%s/sourcegear.sqlite3/%s/runtimes/win-x64/native/e_sqlite3.dll\n' \
    "$TUFREPLAY_NUGET_PACKAGES_DIR" "$version"
}
