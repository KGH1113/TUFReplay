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

OUT="${TUFREPLAY_BUILD_DIR:-$PROJECT/build/TUFReplay}"
BOOTSTRAP_OUT="${TUFREPLAY_BOOTSTRAP_BUILD_DIR:-$PROJECT/build/TUFReplay.Bootstrap}"
DEST="${TUFREPLAY_INSTALL_DIR:-$ADOFAI_MODS_DIR/TUFReplay}"

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

require_file "$DOTNET_EXE"
require_dir "$ADOFAI_MANAGED"
require_file "$UNITY_MOD_MANAGER_DLL"
require_file "$HARMONY_DLL"
require_file "$ADOFAI_IPC_DLL"
require_file "$ADOFAI_IPC_BOOTSTRAP_DLL"

MAC_HELPER_APP="$PROJECT/build/macos-helper/TUFReplayMicrophoneCapture.app"
if [ "$(uname -s)" = "Darwin" ]; then
  "$PROJECT/scripts/build-macos-microphone-helper.sh"
  require_dir "$MAC_HELPER_APP"
fi

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
"$DOTNET_EXE" build "$PROJECT/TUFReplay.Bootstrap/TUFReplay.Bootstrap.csproj" \
  -p:OutputPath="$BOOTSTRAP_OUT/" \
  -p:AdofaiManaged="$ADOFAI_MANAGED" \
  -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL"

DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
"$DOTNET_EXE" build "$PROJECT/TUFReplay/TUFReplay.csproj" \
  -p:OutputPath="$OUT/" \
  -p:AdofaiManaged="$ADOFAI_MANAGED" \
  -p:AdofaiMods="$ADOFAI_MODS_DIR" \
  -p:UnityModManagerDll="$UNITY_MOD_MANAGER_DLL" \
  -p:HarmonyDll="$HARMONY_DLL" \
  -p:AdofaiIpcDll="$ADOFAI_IPC_DLL"

if [ "$(uname -s)" = "Darwin" ]; then
  SOURCEGEAR_VERSION="$(sed -n 's/.*PackageReference Include="SourceGear\.sqlite3" Version="\([^"]*\)".*/\1/p' "$PROJECT/TUFReplay/TUFReplay.csproj" | head -n 1)"
  TEST_SQLITE_ARCH="$(uname -m)"
  if [ "$TEST_SQLITE_ARCH" = "x86_64" ]; then TEST_SQLITE_ARCH="x64"; fi
  TEST_SQLITE_LIBRARY="${NUGET_PACKAGES:-$HOME/.nuget/packages}/sourcegear.sqlite3/$SOURCEGEAR_VERSION/runtimes/osx-$TEST_SQLITE_ARCH/native/libe_sqlite3.dylib"
  require_file "$TEST_SQLITE_LIBRARY"
  TUFREPLAY_SQLITE_NATIVE_LIBRARY="$TEST_SQLITE_LIBRARY" \
  DOTNET_ROOT="$DOTNET_ROOT" DOTNET_ROOT_ARM64="$DOTNET_ROOT_ARM64" \
  "$DOTNET_EXE" run --project "$PROJECT/TUFReplay.Tests/TUFReplay.Tests.csproj" \
    -p:TUFReplayDll="$OUT/TUFReplay.dll"
fi

mkdir -p "$DEST"
rm -rf "$DEST/assembly_cache"
rm -rf "$DEST/dependency"
rm -rf "$DEST/.tufreplay-update"
rm -f "$DEST/JAModInfo.json" "$DEST/JAMod.Bootstrap.dll"
rm -f "$DEST"/JAMod.Bootstrap.dll.*.cache
cp "$PROJECT/TUFReplay/Info.json" "$DEST/"
cp "$PROJECT/TUFReplay/AdofaiIpcBootstrap.json" "$DEST/"
cp "$PROJECT/THIRD_PARTY_NOTICES.md" "$DEST/"
cp "$OUT/TUFReplay.dll" "$DEST/"
cp "$BOOTSTRAP_OUT/TUFReplay.Bootstrap.dll" "$DEST/"
cp "$ADOFAI_IPC_BOOTSTRAP_DLL" "$DEST/"

if [ -d "$PROJECT/TUFReplay/Assets" ]; then
  rm -rf "$DEST/Assets"
  cp -R "$PROJECT/TUFReplay/Assets" "$DEST/"
fi

if [ "$(uname -s)" = "Darwin" ]; then
  rm -rf "$DEST/Helpers/mac"
  mkdir -p "$DEST/Helpers/mac"
  cp -R "$MAC_HELPER_APP" "$DEST/Helpers/mac/"
fi

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
    cp "$OUT/$dll" "$DEST/"
  fi
done

if [ -n "${SQLITE_NATIVE_LIBRARY:-}" ]; then
  require_file "$SQLITE_NATIVE_LIBRARY"

  SQLITE_DEST="$DEST/$(basename "$SQLITE_NATIVE_LIBRARY")"
  cp "$SQLITE_NATIVE_LIBRARY" "$SQLITE_DEST"
  chmod +x "$SQLITE_DEST" 2>/dev/null || true
  xattr -d com.apple.quarantine "$SQLITE_DEST" 2>/dev/null || true
  xattr -d com.apple.provenance "$SQLITE_DEST" 2>/dev/null || true
fi

if [ -f "$OUT/TUFReplay.pdb" ]; then
  cp "$OUT/TUFReplay.pdb" "$DEST/"
fi

echo "Installed to $DEST"
