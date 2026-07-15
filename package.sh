#!/usr/bin/env bash
set -euo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -f "$PROJECT/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  source "$PROJECT/.env"
  set +a
fi

ADOFAI_DIR="${ADOFAI_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
ADOFAI_MODS_DIR="${ADOFAI_MODS_DIR:-$ADOFAI_DIR/Mods}"
ADOFAI_MANAGED="${ADOFAI_MANAGED:-$ADOFAI_DIR/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed}"

DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET_ROOT_ARM64="${DOTNET_ROOT_ARM64:-$DOTNET_ROOT}"
DOTNET_EXE="${DOTNET_EXE:-$DOTNET_ROOT/dotnet}"

UNITY_MOD_MANAGER_DLL="${UNITY_MOD_MANAGER_DLL:-$ADOFAI_MANAGED/UnityModManager/UnityModManager.dll}"
HARMONY_DLL="${HARMONY_DLL:-$ADOFAI_MANAGED/UnityModManager/0Harmony.dll}"
ADOFAI_IPC_DLL="${ADOFAI_IPC_DLL:-${ADOFIA_IPC_DLL:-$ADOFAI_MODS_DIR/AdofaiIpc/AdofaiIpc.dll}}"
ADOFAI_IPC_BOOTSTRAP_DLL="${ADOFAI_IPC_BOOTSTRAP_DLL:-$ADOFAI_MODS_DIR/AdofaiIpc/AdofaiIpc.Bootstrap.dll}"
ADOFAI_IPC_INFO_JSON="${ADOFAI_IPC_INFO_JSON:-$ADOFAI_MODS_DIR/AdofaiIpc/Info.json}"
ADOFAI_IPC_BOOTSTRAP_LOCK="$PROJECT/TUFReplay/AdofaiIpcBootstrap.lock"

OUT="${TUFREPLAY_BUILD_DIR:-$PROJECT/build/TUFReplay}"
PACKAGE_ROOT="${TUFREPLAY_PACKAGE_ROOT:-$PROJECT/build/package}"
STAGE="$PACKAGE_ROOT/TUFReplay"
ZIP_PATH="${TUFREPLAY_PACKAGE_ZIP:-$PROJECT/build/TUFReplay.zip}"
NUGET_PACKAGES_DIR="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
SOURCEGEAR_SQLITE3_VERSION="${SOURCEGEAR_SQLITE3_VERSION:-}"
if [ -z "$SOURCEGEAR_SQLITE3_VERSION" ]; then
  SOURCEGEAR_SQLITE3_VERSION="$(sed -n 's/.*PackageReference Include="SourceGear\.sqlite3" Version="\([^"]*\)".*/\1/p' "$PROJECT/TUFReplay/TUFReplay.csproj" | head -n 1)"
fi
WIN_SQLITE_DLL="$NUGET_PACKAGES_DIR/sourcegear.sqlite3/$SOURCEGEAR_SQLITE3_VERSION/runtimes/win-x64/native/e_sqlite3.dll"

require_file() {
  if [ ! -f "$1" ]; then
    echo "Missing required file: $1" >&2
    exit 1
  fi
}

require_dir() {
  if [ ! -d "$1" ]; then
    echo "Missing required directory: $1" >&2
    exit 1
  fi
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

if [ -z "$SOURCEGEAR_SQLITE3_VERSION" ]; then
  echo "Missing SourceGear.sqlite3 PackageReference version in TUFReplay.csproj" >&2
  exit 1
fi

require_command zip
require_command shasum
require_file "$DOTNET_EXE"
require_dir "$ADOFAI_MANAGED"
require_file "$UNITY_MOD_MANAGER_DLL"
require_file "$HARMONY_DLL"
require_file "$ADOFAI_IPC_DLL"
require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"
require_file "$ADOFAI_IPC_INFO_JSON"
require_file "$ADOFAI_IPC_BOOTSTRAP_LOCK"

# shellcheck disable=SC1090
source "$ADOFAI_IPC_BOOTSTRAP_LOCK"

installed_ipc_version="$(sed -n 's/.*"Version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$ADOFAI_IPC_INFO_JSON" | head -n 1)"
if [ "$installed_ipc_version" != "$ADOFAIIPC_VERSION" ]; then
  echo "AdofaiIpc version mismatch: expected $ADOFAIIPC_VERSION, found ${installed_ipc_version:-unknown}" >&2
  exit 1
fi

bootstrap_sha256="$(shasum -a 256 "$ADOFAI_IPC_BOOTSTRAP_DLL" | awk '{print $1}')"
if [ "$bootstrap_sha256" != "$ADOFAIIPC_BOOTSTRAP_SHA256" ]; then
  echo "AdofaiIpc Bootstrap checksum mismatch." >&2
  echo "Expected: $ADOFAIIPC_BOOTSTRAP_SHA256" >&2
  echo "Actual:   $bootstrap_sha256" >&2
  exit 1
fi

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
"$DOTNET_EXE" build "$PROJECT/TUFReplay/TUFReplay.csproj" \
  -p:OutputPath="$OUT/" \
  -p:AdofaiManaged="$ADOFAI_MANAGED" \
  -p:AdofaiMods="$ADOFAI_MODS_DIR" \
  -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
  -p:HarmonyDll="$HARMONY_DLL" \
  -p:AdofaiIpcDll="$ADOFAI_IPC_DLL"

require_file "$WIN_SQLITE_DLL"

rm -rf "$STAGE"
mkdir -p "$STAGE"

cp "$PROJECT/TUFReplay/Info.json" "$STAGE/"
cp "$PROJECT/TUFReplay/AdofaiIpcBootstrap.json" "$STAGE/"
cp "$OUT/TUFReplay.dll" "$STAGE/"
cp "$ADOFAI_IPC_BOOTSTRAP_DLL" "$STAGE/"
cp "$WIN_SQLITE_DLL" "$STAGE/e_sqlite3.dll"

for dll in \
  Microsoft.Data.Sqlite.dll \
  SQLitePCLRaw.core.dll \
  SQLitePCLRaw.provider.dynamic_cdecl.dll \
  System.Buffers.dll \
  System.Memory.dll \
  System.Numerics.Vectors.dll \
  System.Runtime.CompilerServices.Unsafe.dll
do
  if [ -f "$OUT/$dll" ]; then
    cp "$OUT/$dll" "$STAGE/"
  fi
done

if [ -f "$OUT/TUFReplay.pdb" ]; then
  cp "$OUT/TUFReplay.pdb" "$STAGE/"
fi

rm -f "$ZIP_PATH"
mkdir -p "$(dirname "$ZIP_PATH")"
(
  cd "$PACKAGE_ROOT"
  zip -r "$ZIP_PATH" TUFReplay \
    -x 'TUFReplay/Data/*' \
    -x 'TUFReplay/*.sqlite' \
    -x 'TUFReplay/*.sqlite3' \
    -x 'TUFReplay/*.db' \
    -x 'TUFReplay/*.db-shm' \
    -x 'TUFReplay/*.db-wal' \
    -x 'TUFReplay/*.log'
)

echo "Packaged to $ZIP_PATH"
