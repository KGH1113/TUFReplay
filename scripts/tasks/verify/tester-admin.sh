#!/usr/bin/env bash
set -euo pipefail
TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"
require_command bun
cd "$TUFREPLAY_PROJECT_ROOT"
bun run --cwd tools/trusted-testers-admin test
bun run --cwd tools/trusted-testers-admin typecheck
bun test tools/testing/trusted-testers-admin.integration.test.ts
