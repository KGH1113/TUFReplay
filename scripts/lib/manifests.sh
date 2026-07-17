#!/usr/bin/env bash

if [ "${TUFREPLAY_MANIFESTS_LOADED:-0}" = "1" ]; then
  return 0
fi
TUFREPLAY_MANIFESTS_LOADED=1

TUFREPLAY_RUNTIME_DLLS=(
  Microsoft.Data.Sqlite.dll
  SQLitePCLRaw.core.dll
  SQLitePCLRaw.provider.dynamic_cdecl.dll
  System.Buffers.dll
  System.Memory.dll
  System.Numerics.Vectors.dll
  System.Runtime.CompilerServices.Unsafe.dll
)

TUFREPLAY_UI_BUNDLES=(
  mac/tufreplay_ui.bundle
  win/tufreplay_ui.bundle
  linux/tufreplay_ui.bundle
)
