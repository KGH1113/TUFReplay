#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command shasum
require_file "$ADOFAI_IPC_BOOTSTRAP_LOCK"
require_file "$ADOFAI_IPC_INFO_JSON"
require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"

# shellcheck disable=SC1090
source "$ADOFAI_IPC_BOOTSTRAP_LOCK"

installed_version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$ADOFAI_IPC_INFO_JSON" | head -n 1)"
if [ "$installed_version" != "$ADOFAIIPC_VERSION" ]; then
  fail "AdofaiIpc version mismatch: expected $ADOFAIIPC_VERSION, found ${installed_version:-unknown}"
fi

bootstrap_sha256="$(shasum -a 256 "$ADOFAI_IPC_BOOTSTRAP_DLL" | awk '{print $1}')"
if [ "$bootstrap_sha256" != "$ADOFAIIPC_BOOTSTRAP_SHA256" ]; then
  printf 'Expected: %s\n' "$ADOFAIIPC_BOOTSTRAP_SHA256" >&2
  printf 'Actual:   %s\n' "$bootstrap_sha256" >&2
  fail "AdofaiIpc Bootstrap checksum mismatch."
fi
