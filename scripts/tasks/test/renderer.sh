#!/usr/bin/env bash
set -euo pipefail
TASK_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$TASK_DIR/../../lib/context.sh"
"$DOTNET_EXE" run --project "$TUFREPLAY_PROJECT_ROOT/TUFReplay.Tests/TUFReplay.Tests.csproj" \
  -p:TUFReplayDll="$TUFREPLAY_BUILD_OUTPUT/TUFReplay.dll" \
  -p:AdofaiIpcBundle="$ADOFAI_IPC_BUNDLE" \
  -p:TUFReplayBootstrapDll="$TUFREPLAY_BOOTSTRAP_BUILD_OUTPUT/TUFReplay.SdkLauncher.dll" \
  -p:TUFReplayUpdateEngineDll="$TUFREPLAY_UPDATE_ENGINE_BUILD_OUTPUT/TUFReplay.UpdateEngine.dll" \
  -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
  -p:AdofaiManaged="$ADOFAI_MANAGED" -- --renderer
