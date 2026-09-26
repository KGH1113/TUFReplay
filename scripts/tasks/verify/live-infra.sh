#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$ROOT"
case "${1:-status}" in
  up) docker compose up -d --build --wait replay-objects replay-cdn ;;
  stop) docker compose stop replay-cdn replay-objects ;;
  status) docker compose ps replay-objects replay-cdn ;;
  prepare) bun tools/live-e2e/maintenance.ts ;;
  charts) bun tools/live-e2e/prepare.ts "${@:2}" ;;
  check) bun test tools/live-e2e/storage.test.ts tools/live-e2e/official-chart.test.ts tools/live-e2e/archive-server.test.ts tools/live-e2e/catalog-update.test.ts deploy/replay-cdn/worker.test.mjs
    docker compose exec -T replay-cdn node check.mjs
    export LOCAL_OBJECT_STORE
    LOCAL_OBJECT_STORE="$(bun -e 'import s from "./tools/live-e2e/services.json"; console.log(s.objectStore)')"
    "$ROOT/scripts/run.sh" server-check local_s3_multipart_roundtrip -- --ignored ;;
  *) printf 'Usage: ./scripts/run.sh live-infra [up|stop|status|check|prepare|charts]\n' >&2; exit 2 ;;
esac
