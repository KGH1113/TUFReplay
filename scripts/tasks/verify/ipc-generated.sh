#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$TASK_DIR/../../.." && pwd)"
cd "$PROJECT_ROOT"
node node_modules/@adofai-ipc/typespec/bin/adofai-ipc.mjs generate \
  --config adofai-ipc.config.json "$@"
