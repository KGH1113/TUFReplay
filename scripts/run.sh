#!/usr/bin/env bash
set -euo pipefail

SCRIPTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/logging.sh
source "$SCRIPTS_DIR/lib/logging.sh"

usage() {
  cat <<'USAGE'
Usage: ./scripts/run.sh <command>

Commands:
  build       Build, test, and install the mod
  mod-check   Build and test the mod without installing it
  visual-import Run the test-only visual importer stdin bridge (after mod-check)
  visual-check Check source contracts across the mod, companion and sibling replay consumers
  visual-fixtures Generate real-asset importer fixtures for cross-repository tests (after mod-check)
  visual-pipeline-check Run importer, isolated database/API, and renderer pixel checks
  visual-font-assets Rebuild the bundled CJK WOFF from a local original game font
  package     Build the release package and metadata
  web-check   Run web tests, typecheck, Biome, and production build
  server-check Check Rust formatting, all targets, and library tests (--format / --clippy)
  cdn-check   Check signed R2 edge delivery and cache authorization
  live-infra  Manage persistent local object storage/CDN (up|stop|status|check)
  tester-admin-check Check the separate tester admin app and optional PostgreSQL integration
  mac-helper  Build and verify the macOS microphone helper
  unity-ui    Rebuild the Unity runtime prefab and platform UI bundles
  check       Validate all shell scripts
  help        Show this help
USAGE
}

command_name="${1:-help}"
case "$command_name" in
  build)
    exec "$SCRIPTS_DIR/workflows/build-install.sh"
    ;;
  mod-check)
    exec "$SCRIPTS_DIR/workflows/build-install.sh" --no-install
    ;;
  visual-import)
    exec dotnet "$SCRIPTS_DIR/../TUFReplay.Tests/bin/Debug/net8.0/TUFReplay.Tests.dll" --visual-import
    ;;
  visual-check)
    exec bun test "$SCRIPTS_DIR/../tools/live-e2e/visual-source-contract.test.ts"
    ;;
  visual-fixtures)
    exec bun "$SCRIPTS_DIR/../tools/live-e2e/visual-fixtures.ts"
    ;;
  visual-pipeline-check)
    exec bash "$SCRIPTS_DIR/tasks/verify/visual-pipeline.sh"
    ;;
  visual-font-assets)
    exec python3 "$SCRIPTS_DIR/tasks/build/visual-font-assets.py" "${@:2}"
    ;;
  package)
    exec "$SCRIPTS_DIR/workflows/package-release.sh"
    ;;
  web-check)
    run_task "Verify companion web workspace" "$SCRIPTS_DIR/tasks/verify/web.sh"
    ;;
  server-check)
    exec bash "$SCRIPTS_DIR/tasks/verify/server.sh" "${@:2}"
    ;;
  cdn-check)
    exec bun test "$SCRIPTS_DIR/../deploy/replay-cdn/worker.test.mjs"
    ;;
  live-infra)
    exec bash "$SCRIPTS_DIR/tasks/verify/live-infra.sh" "${@:2}"
    ;;
  tester-admin-check)
    exec bash "$SCRIPTS_DIR/tasks/verify/tester-admin.sh"
    ;;
  mac-helper)
    run_task "Build macOS microphone helper" "$SCRIPTS_DIR/tasks/build/macos-microphone-helper.sh"
    ;;
  unity-ui)
    run_task "Build Unity replay timeline bundles" "$SCRIPTS_DIR/tasks/build/unity-ui.sh"
    ;;
  check)
    exec "$SCRIPTS_DIR/workflows/check-scripts.sh"
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    printf 'Unknown command: %s\n\n' "$command_name" >&2
    usage >&2
    exit 2
    ;;
esac
