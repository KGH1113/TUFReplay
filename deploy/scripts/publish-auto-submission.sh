#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "$#" != 3 ]]; then
  echo "Usage: publish-auto-submission.sh <full-git-sha> <version> <upload-id>" >&2
  exit 64
fi

DEPLOY_SHA="$1"
PACKAGE_VERSION="$2"
UPLOAD_ID="$3"
if [[ ! "$DEPLOY_SHA" =~ ^[0-9a-f]{40}$ ]]; then
  echo "Auto-submission publishing requires a full lowercase Git commit SHA." >&2
  exit 64
fi
if [[ ! "$PACKAGE_VERSION" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-auto-submission\.(0|[1-9][0-9]*)$ ]]; then
  echo "Auto-submission package version has an invalid format." >&2
  exit 64
fi
if [[ ! "$UPLOAD_ID" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$ ]]; then
  echo "Auto-submission upload identifier has an invalid format." >&2
  exit 64
fi

DEPLOY_DIR="/home/kgh/tuf-replay-environments/auto-submission"
DATA_DIR="/home/kgh/tuf-replay-data/auto-submission"
UPDATE_ROOT="$DATA_DIR/updates"
INCOMING_ROOT="$DATA_DIR/incoming"
INCOMING_DIR="$INCOMING_ROOT/$UPLOAD_ID"
PACKAGE_ZIP="$INCOMING_DIR/TUFReplay.zip"
PACKAGE_MANIFEST="$INCOMING_DIR/TUFReplay.update.json"

for command_name in git curl python3 stat flock; do
  command -v "$command_name" >/dev/null 2>&1 || {
    printf 'Missing required command: %s\n' "$command_name" >&2
    exit 1
  }
done

umask 077
exec 9>"$DATA_DIR/publish.lock"
flock -n 9 || {
  echo "Another auto-submission package promotion is already running." >&2
  exit 1
}

if [[ "$(git -C "$DEPLOY_DIR" rev-parse HEAD)" != "$DEPLOY_SHA" ]]; then
  echo "The auto-submission checkout does not match the requested deployment commit." >&2
  exit 1
fi
if [[ -L "$INCOMING_ROOT" || ! -d "$INCOMING_ROOT" || -L "$INCOMING_DIR" || ! -d "$INCOMING_DIR" ]]; then
  echo "Auto-submission upload directory is missing or unsafe." >&2
  exit 1
fi
incoming_mode="$(stat -c '%a' "$INCOMING_DIR")"
if [[ "$incoming_mode" != "700" ]]; then
  echo "Auto-submission upload directory permissions must be 700." >&2
  exit 1
fi
for staged_file in "$PACKAGE_ZIP" "$PACKAGE_MANIFEST"; do
  if [[ -L "$staged_file" || ! -f "$staged_file" ]]; then
    echo "A required staged package file is missing or unsafe." >&2
    exit 1
  fi
  staged_mode="$(stat -c '%a' "$staged_file")"
  if [[ "$staged_mode" != "600" && "$staged_mode" != "400" ]]; then
    echo "Auto-submission staged package files must have mode 600 or 400." >&2
    exit 1
  fi
done

public_metadata="$(mktemp "$DATA_DIR/.deployment.XXXXXX")"
if ! curl --fail --silent --show-error --max-time 15 \
  "https://tufreplay-auto.impl1113.dev/deployment.json" -o "$public_metadata"; then
  rm -f "$public_metadata"
  echo "Auto-submission deployment metadata could not be verified." >&2
  exit 1
fi
if ! python3 - "$public_metadata" "$DEPLOY_SHA" <<'PY'
import json
import sys

try:
    with open(sys.argv[1], encoding="utf-8") as stream:
        metadata = json.load(stream)
except (OSError, ValueError):
    raise SystemExit("Auto-submission deployment metadata is invalid.")
if (
    metadata.get("environment") != "auto-submission"
    or metadata.get("buildFlavor") != "auto-submission"
    or metadata.get("gitSHA") != sys.argv[2]
):
    raise SystemExit("Auto-submission public site does not match the requested build SHA.")
PY
then
  rm -f "$public_metadata"
  echo "Auto-submission public site does not match the requested package build." >&2
  exit 1
fi
rm -f "$public_metadata"

mkdir -p "$UPDATE_ROOT/versions"
chmod 0755 "$UPDATE_ROOT" "$UPDATE_ROOT/versions"

python3 - "$PACKAGE_ZIP" "$PACKAGE_MANIFEST" "$PACKAGE_VERSION" "$DEPLOY_SHA" "$UPDATE_ROOT" <<'PY'
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
from pathlib import Path

package_path = Path(sys.argv[1])
source_manifest_path = Path(sys.argv[2])
expected_version, build_sha = sys.argv[3:5]
update_root = Path(sys.argv[5])
version_pattern = re.compile(
    r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-auto-submission\.(0|[1-9][0-9]*)"
)

def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

def version_key(version: str) -> tuple[int, int, int, int]:
    match = version_pattern.fullmatch(version)
    if match is None:
        raise ValueError("invalid auto-submission version")
    return tuple(int(part) for part in match.groups())

try:
    with source_manifest_path.open(encoding="utf-8") as source:
        manifest = json.load(source)
except (OSError, ValueError):
    raise SystemExit("Auto-submission package manifest is invalid.")

package_bytes = package_path.stat().st_size
package_sha = sha256_file(package_path)
expected_url = f"versions/{expected_version}/TUFReplay.zip"
if (
    manifest.get("schemaVersion") != 1
    or manifest.get("version") != expected_version
    or manifest.get("buildFlavor") != "auto-submission"
    or manifest.get("packageAsset") != "TUFReplay.zip"
    or manifest.get("packageUrl") != expected_url
    or manifest.get("runtimePath") != f"TUFReplay/Runtime/versions/{expected_version}"
    or manifest.get("packageBytes") != package_bytes
    or manifest.get("packageSha256") != package_sha
):
    raise SystemExit("Auto-submission ZIP does not match its release manifest.")

published_manifest = dict(manifest)
version_dir = update_root / "versions" / expected_version
if version_dir.is_symlink():
    raise SystemExit("The immutable package version path is unsafe.")
if version_dir.exists():
    existing_zip = version_dir / "TUFReplay.zip"
    existing_manifest_path = version_dir / "TUFReplay.update.json"
    if (
        existing_zip.is_symlink()
        or existing_manifest_path.is_symlink()
        or not existing_zip.is_file()
        or not existing_manifest_path.is_file()
    ):
        raise SystemExit("An incomplete immutable version directory already exists; refusing to replace it.")
    try:
        with existing_manifest_path.open(encoding="utf-8") as existing_manifest_source:
            existing_manifest = json.load(existing_manifest_source)
    except (OSError, ValueError):
        raise SystemExit("The existing immutable package manifest is invalid.")
    if sha256_file(existing_zip) != package_sha or existing_manifest != published_manifest:
        raise SystemExit("This immutable package version already exists with different bytes or manifest.")
else:
    staging_dir = Path(tempfile.mkdtemp(prefix=f".{expected_version}.", dir=update_root / "versions"))
    try:
        shutil.copyfile(package_path, staging_dir / "TUFReplay.zip")
        with (staging_dir / "TUFReplay.update.json").open("w", encoding="utf-8", newline="\n") as output:
            json.dump(published_manifest, output, indent=2, ensure_ascii=False)
            output.write("\n")
        os.chmod(staging_dir / "TUFReplay.zip", 0o644)
        os.chmod(staging_dir / "TUFReplay.update.json", 0o644)
        os.chmod(staging_dir, 0o755)
        os.rename(staging_dir, version_dir)
    except BaseException:
        shutil.rmtree(staging_dir, ignore_errors=True)
        raise

latest_manifest = dict(published_manifest)
latest_manifest["deploymentSha"] = build_sha
latest_path = update_root / "latest.json"
if latest_path.is_symlink():
    raise SystemExit("The latest package manifest path is unsafe.")
if latest_path.exists():
    try:
        with latest_path.open(encoding="utf-8") as latest_source:
            current_latest = json.load(latest_source)
        current_version = current_latest["version"]
        current_key = version_key(current_version)
    except (OSError, ValueError, KeyError, TypeError):
        raise SystemExit("Existing latest package manifest is invalid; refusing to replace it.")
    new_key = version_key(expected_version)
    if new_key < current_key:
        raise SystemExit("Refusing to move latest.json to an older package version.")
    if new_key == current_key and (
        current_version != expected_version
        or current_latest.get("packageSha256") != package_sha
    ):
        raise SystemExit("This package version is already current with different bytes.")

latest_temp = None
try:
    with tempfile.NamedTemporaryFile(
        mode="w", encoding="utf-8", newline="\n", dir=update_root, prefix=".latest.", delete=False
    ) as latest_output:
        latest_temp = Path(latest_output.name)
        json.dump(latest_manifest, latest_output, indent=2, ensure_ascii=False)
        latest_output.write("\n")
    os.chmod(latest_temp, 0o644)
    os.replace(latest_temp, latest_path)
finally:
    if latest_temp is not None:
        latest_temp.unlink(missing_ok=True)

os.chmod(version_dir, 0o755)
print(f"Published immutable auto-submission package {expected_version} ({package_sha}).")
PY

chmod 0755 "$UPDATE_ROOT" "$UPDATE_ROOT/versions" "$UPDATE_ROOT/versions/$PACKAGE_VERSION"
chmod 0644 "$UPDATE_ROOT/versions/$PACKAGE_VERSION/TUFReplay.zip" \
  "$UPDATE_ROOT/versions/$PACKAGE_VERSION/TUFReplay.update.json" "$UPDATE_ROOT/latest.json"
rm -rf -- "$INCOMING_DIR"
