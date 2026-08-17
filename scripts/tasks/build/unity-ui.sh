#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"

unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity}"
if [ ! -x "$unity_editor" ]; then
  printf 'Unity Editor is unavailable: %s\n' "$unity_editor" >&2
  exit 1
fi

"$unity_editor" \
  -batchmode \
  -quit \
  -projectPath "$TUFREPLAY_PROJECT_ROOT/TUFReplay.Unity" \
  -executeMethod TUFReplay.Unity.Editor.ReplayTimelineRuntimeBundleBuilder.BuildRuntimeUiBundles \
  -logFile -
