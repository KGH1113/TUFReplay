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
  package     Build the release package and metadata
  mac-helper  Build and verify the macOS microphone helper
  check       Validate all shell scripts
  help        Show this help
USAGE
}

command_name="${1:-help}"
case "$command_name" in
  build)
    exec "$SCRIPTS_DIR/workflows/build-install.sh"
    ;;
  package)
    exec "$SCRIPTS_DIR/workflows/package-release.sh"
    ;;
  mac-helper)
    run_task "Build macOS microphone helper" "$SCRIPTS_DIR/tasks/build/macos-microphone-helper.sh"
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
