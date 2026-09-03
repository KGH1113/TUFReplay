#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

[ "$#" -gt 0 ] || fail "Usage: unity-mono-compatibility.sh DLL_OR_DIRECTORY_OR_ZIP [...]"

require_command find
require_command grep
require_command unzip

scan_file() {
  local assembly="$1"

  if LC_ALL=C grep -aFq 'DefaultInterpolatedStringHandler' "$assembly"; then
    fail "Unity/Mono-incompatible interpolated string handler found in $assembly"
  fi
  if LC_ALL=C grep -aFq 'ToStringAndClear' "$assembly"; then
    fail "Unity/Mono-incompatible interpolated string handler call found in $assembly"
  fi
}

scan_directory() {
  local directory="$1"
  local assembly_count

  assembly_count="$(find "$directory" -type f -name '*.dll' | wc -l | tr -d '[:space:]')"
  [ "$assembly_count" -gt 0 ] || fail "No managed assemblies found in $directory"
  while IFS= read -r -d '' assembly; do
    scan_file "$assembly"
  done < <(find "$directory" -type f -name '*.dll' -print0)
  printf 'Unity/Mono compatibility verified for %s staged assemblies.\n' "$assembly_count"
}

scan_archive() {
  local archive="$1"
  local assembly_count

  assembly_count="$(unzip -Z1 "$archive" | grep -Ec '\.dll$')"
  [ "$assembly_count" -gt 0 ] || fail "No managed assemblies found in $archive"
  while IFS= read -r entry; do
    if unzip -p "$archive" "$entry" | LC_ALL=C grep -aF 'DefaultInterpolatedStringHandler' >/dev/null; then
      fail "Unity/Mono-incompatible interpolated string handler found in package entry $entry"
    fi
    if unzip -p "$archive" "$entry" | LC_ALL=C grep -aF 'ToStringAndClear' >/dev/null; then
      fail "Unity/Mono-incompatible interpolated string handler call found in package entry $entry"
    fi
  done < <(unzip -Z1 "$archive" | grep -E '\.dll$')
  printf 'Unity/Mono compatibility verified for %s packaged assemblies.\n' "$assembly_count"
}

file_count=0
for target in "$@"; do
  if [ -d "$target" ]; then
    scan_directory "$target"
  elif [ "${target##*.}" = "zip" ]; then
    require_file "$target"
    scan_archive "$target"
  else
    require_file "$target"
    scan_file "$target"
    file_count=$((file_count + 1))
  fi
done

if [ "$file_count" -gt 0 ]; then
  printf 'Unity/Mono compatibility verified for %s build assemblies.\n' "$file_count"
fi
