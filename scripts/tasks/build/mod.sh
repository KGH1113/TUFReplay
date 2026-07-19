#!/usr/bin/env bash
set -euo pipefail

TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../../lib/context.sh
source "$TASK_DIR/../../lib/context.sh"

configuration="${1:-Debug}"

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
  "$DOTNET_EXE" build "$TUFREPLAY_PROJECT_ROOT/TUFReplay/TUFReplay.csproj" \
    --configuration "$configuration" \
    -p:OutputPath="$TUFREPLAY_BUILD_OUTPUT/" \
    -p:AdofaiManaged="$ADOFAI_MANAGED" \
    -p:AdofaiMods="$ADOFAI_MODS_DIR" \
    -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
    -p:HarmonyDll="$HARMONY_DLL" \
    -p:AdofaiIpcDll="$ADOFAI_IPC_DLL"
