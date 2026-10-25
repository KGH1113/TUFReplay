#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

artifact_path="${1:-$TUFREPLAY_WIN_NATIVE_LIBRARY}"
require_file "$artifact_path"

objdump_tool=""
for candidate in llvm-objdump objdump; do
  if command -v "$candidate" >/dev/null 2>&1; then
    objdump_tool="$candidate"
    break
  fi
done
[ -n "$objdump_tool" ] || fail "Missing required command: llvm-objdump or objdump"

headers="$("$objdump_tool" -p "$artifact_path")"
printf '%s\n' "$headers" | grep -Eq 'x86-64|x86_64|pe-x86-64|pei-x86-64' \
  || fail "TUFReplayNative.dll is not an x86-64 Windows image."

required_exports=(
  tufreplay_win_native_abi_version
  tufreplay_win_input_create
  tufreplay_win_input_start
  tufreplay_win_input_stop
  tufreplay_win_input_destroy
  tufreplay_win_input_is_running
  tufreplay_win_input_last_error
  tufreplay_win_input_last_system_error
  tufreplay_win_input_wait_dequeue
  tufreplay_win_input_take_dropped
  tufreplay_win_input_get_stats
  tufreplay_win_audio_capture_transport
)
for symbol in "${required_exports[@]}"; do
  printf '%s\n' "$headers" | grep -Eq "[[:space:]]$symbol\$" || fail "TUFReplayNative.dll does not export $symbol."
done

# Only system DLLs: the library must not need a separately installed runtime.
while IFS= read -r dependency; do
  case "$dependency" in
    KERNEL32.dll | USER32.dll | ole32.dll | api-ms-win-crt-*.dll) ;;
    *) fail "TUFReplayNative.dll depends on an unexpected library: $dependency" ;;
  esac
done < <(printf '%s\n' "$headers" | sed -n 's/^[[:space:]]*DLL Name: //p')

printf 'Validated %s\n' "$artifact_path"
