#!/usr/bin/env bash
# Bundled IPC v2. No dependency installation or loaded-runtime replacement.
verify_ipc_checksums() {
  local directory="$1"
  require_file "$directory/SHA256SUMS"
  if command -v shasum >/dev/null 2>&1; then
    (cd "$directory" && shasum -a 256 -c SHA256SUMS)
  else
    require_command sha256sum
    (cd "$directory" && tr -d '\r' < SHA256SUMS | sha256sum -c -)
  fi
}
verify_ipc_bundle() {
  verify_ipc_checksums "$ADOFAI_IPC_BUNDLE"
}
copy_ipc_bundle() {
  local destination="$1" name temporary
  for name in AdofaiIpc.Contracts.dll AdofaiIpc.Loader.dll ipc/AdofaiIpc.Runtime.dll ipc/manifest.json; do
    mkdir -p "$destination/$(dirname "$name")"
    temporary="$(mktemp "$destination/$name.XXXXXX")"
    cp "$ADOFAI_IPC_BUNDLE/$name" "$temporary"
    mv -f "$temporary" "$destination/$name"
  done
  mkdir -p "$destination/Notices"
  cp "$ADOFAI_IPC_BUNDLE/LICENSE" "$destination/Notices/AdofaiIpc-LICENSE"
}
