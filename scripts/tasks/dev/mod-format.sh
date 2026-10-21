#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"
# shellcheck source=../../lib/guards.sh
source "$TASK_DIR/../../lib/guards.sh"

mode="${1:-format}"
case "$mode" in
  format|check) ;;
  *) fail "Use mod-format or mod-format check." ;;
esac
require_command git
require_executable "$DOTNET_EXE"
cd "$TUFREPLAY_PROJECT_ROOT"
files=()
while IFS= read -r -d '' path; do
  if [ -f "$path" ]; then
    files+=("$path")
  fi
done < <(git ls-files --deduplicate --modified --others --exclude-standard -z -- '*.cs')
if [ "${#files[@]}" -eq 0 ]; then
  printf 'No changed C# files.\n'
  exit 0
fi
"$DOTNET_EXE" tool restore
"$DOTNET_EXE" csharpier "$mode" "${files[@]}"
