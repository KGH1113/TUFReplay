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
  mod-format  Format changed C# files (use 'mod-format check' to verify)
  camera-copy-bench  Compare camera frame copy paths in standalone Mono
  package     Build the release package and metadata
  web-test    Run web unit and contract tests (optional test filters)
  web-format  Format the companion web source
  web-lock    Refresh the local web dependency lockfile
  web-check   Run web tests, typecheck, Biome, and production build
  web-format  Format and apply safe lint fixes to the web workspace
  web-dev     Run the companion web development server (extra arguments go to Vite)
  mac-helper  Build and verify the macOS microphone/webcam helper
  unity-ui    Rebuild the Unity runtime prefab and platform UI bundles
  source-format Format C# source with the repository tool
  source-check  Check C# source formatting
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
  mod-format)
    shift
    exec bash "$SCRIPTS_DIR/tasks/dev/mod-format.sh" "$@"
    ;;
  camera-copy-bench)
    shift
    exec bash "$SCRIPTS_DIR/tasks/dev/camera-copy-bench.sh" "$@"
    ;;
  package)
    exec "$SCRIPTS_DIR/workflows/package-release.sh"
    ;;
  web-test)
    exec bun run --cwd "$SCRIPTS_DIR/../web" test "${@:2}"
    ;;
  web-format)
    exec bun run --cwd "$SCRIPTS_DIR/../web" format
    ;;
  web-lock)
    exec bun install --cwd "$SCRIPTS_DIR/../web" --lockfile-only
    ;;
  web-check)
    run_task "Verify companion web workspace" "$SCRIPTS_DIR/tasks/verify/web.sh"
    ;;
  web-format)
    run_task "Format companion web workspace" bash "$SCRIPTS_DIR/tasks/dev/web-format.sh"
    ;;
  web-dev)
    shift
    exec bash "$SCRIPTS_DIR/tasks/dev/web.sh" "$@"
    ;;
  mac-helper)
    run_task "Build macOS microphone helper" "$SCRIPTS_DIR/tasks/build/macos-microphone-helper.sh"
    ;;
  unity-ui)
    run_task "Build Unity replay timeline bundles" "$SCRIPTS_DIR/tasks/build/unity-ui.sh"
    ;;
  source-format|source-check)
    source "$SCRIPTS_DIR/lib/context.sh"
    format_action="format"
    if [[ "$command_name" == "source-check" ]]; then format_action="check"; fi
    exec "$DOTNET_EXE" csharpier "$format_action" "$TUFREPLAY_PROJECT_ROOT/TUFReplay" "$TUFREPLAY_PROJECT_ROOT/TUFReplay.Tests"
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
