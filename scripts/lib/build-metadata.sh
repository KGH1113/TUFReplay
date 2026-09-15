#!/usr/bin/env bash

if [ "${TUFREPLAY_BUILD_METADATA_LOADED:-0}" = "1" ]; then
  return 0
fi
TUFREPLAY_BUILD_METADATA_LOADED=1

tufreplay_resolve_build_version() {
  local source_info="$1"
  command -v python3 >/dev/null 2>&1 || fail "python3 is required to create package metadata."

  python3 - "$source_info" "$TUFREPLAY_BUILD_VERSION" "$TUFREPLAY_BUILD_FLAVOR" <<'PY'
import json
import re
import sys

source_info, override, flavor = sys.argv[1:]
try:
    with open(source_info, encoding="utf-8-sig") as source:
        info = json.load(source)
except (OSError, ValueError) as error:
    raise SystemExit(f"Could not read TUFReplay Info.json: {error}")

if flavor not in ("standard", "auto-submission"):
    raise SystemExit("TUFREPLAY_BUILD_FLAVOR must be either 'standard' or 'auto-submission'.")

version = override or info.get("Version", "")
if not isinstance(version, str) or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?", version):
    raise SystemExit("The TUFReplay package version must be a canonical semantic version.")
if flavor == "auto-submission" and not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+-auto-submission\.[0-9]+", version):
    raise SystemExit("Auto-submission packages require TUFREPLAY_BUILD_VERSION in the form 0.2.0-auto-submission.N.")
if flavor == "standard" and "-auto-submission." in version:
    raise SystemExit("An auto-submission package version cannot be used for the standard build flavor.")

print(version)
PY
}

tufreplay_write_build_info() {
  local source_info="$1"
  local destination_info="$2"
  local version="$3"

  command -v python3 >/dev/null 2>&1 || fail "python3 is required to create package metadata."

  python3 - "$source_info" "$destination_info" "$version" "$TUFREPLAY_BUILD_FLAVOR" <<'PY'
import json
import sys

source_path, destination_path, version, flavor = sys.argv[1:]
try:
    with open(source_path, encoding="utf-8-sig") as source:
        info = json.load(source)
except (OSError, ValueError) as error:
    raise SystemExit(f"Could not read TUFReplay Info.json: {error}")

info["Version"] = version
info["BuildFlavor"] = flavor
with open(destination_path, "w", encoding="utf-8", newline="\n") as destination:
    json.dump(info, destination, indent=2, ensure_ascii=False)
    destination.write("\n")
PY
}

tufreplay_read_build_info() {
  local info_path="$1"
  command -v python3 >/dev/null 2>&1 || fail "python3 is required to validate package metadata."

  python3 - "$info_path" <<'PY'
import json
import sys

try:
    with open(sys.argv[1], encoding="utf-8-sig") as source:
        info = json.load(source)
except (OSError, ValueError) as error:
    raise SystemExit(f"Could not read staged TUFReplay Info.json: {error}")

version = info.get("Version")
flavor = info.get("BuildFlavor", "standard")
if not isinstance(version, str) or not version:
    raise SystemExit("Staged TUFReplay Info.json has no version.")
if flavor not in ("standard", "auto-submission"):
    raise SystemExit("Staged TUFReplay Info.json has an unsupported build flavor.")
print(f"{version}\t{flavor}")
PY
}
