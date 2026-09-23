#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

require_command cargo
cd "$TUFREPLAY_PROJECT_ROOT/server"
cargo fmt --all -- --check
cargo check --all-targets
if [[ "${1:-}" == "--integration" ]]; then
  shift
  cargo test --test mod "$@"
else
  cargo test --lib "$@"
fi
