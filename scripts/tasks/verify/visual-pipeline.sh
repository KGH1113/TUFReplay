#!/usr/bin/env bash
set -euo pipefail
TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
# Loco's test harness recreates this database. Never infer a development DB.
: "${TUF_VISUAL_TEST_DATABASE_URL:?Set an isolated disposable visual test database URL}"
export DATABASE_URL="$TUF_VISUAL_TEST_DATABASE_URL"
cd "$TUFREPLAY_PROJECT_ROOT"
./scripts/run.sh visual-fixtures
./scripts/run.sh server-check --integration real_importer_visuals_survive_registration_submission_and_public_api -- --ignored
bun test tools/live-e2e/visual-pixels.test.ts
