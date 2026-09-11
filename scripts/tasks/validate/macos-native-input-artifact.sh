#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

artifact_path="${1:-$TUFREPLAY_MAC_INPUT_LIBRARY}"
require_file "$artifact_path"
require_command lipo
require_command nm
lipo "$artifact_path" -verify_arch arm64 x86_64

required_symbols=(
  _tufreplay_input_abi_version
  _tufreplay_input_check_access
  _tufreplay_input_request_access
  _tufreplay_input_create
  _tufreplay_input_start
  _tufreplay_input_stop
  _tufreplay_input_destroy
  _tufreplay_input_wait_dequeue
  _tufreplay_input_copy_state
  _tufreplay_input_take_dropped
)
symbols="$(nm -gU "$artifact_path")"
for symbol in "${required_symbols[@]}"; do
  if ! grep -q " $symbol$" <<<"$symbols"; then
    fail "Missing macOS native input ABI symbol: $symbol"
  fi
done
