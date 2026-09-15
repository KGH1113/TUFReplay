#!/usr/bin/env bash
set -euo pipefail
# One-time administrator setup. Does not change routing or deploy applications.
[[ "$EUID" == 0 ]] || { echo "Administrator authentication is required." >&2; exit 1; }
SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET=/usr/local/sbin/tuf-replay-provision-routing
RULE=/etc/sudoers.d/tuf-replay-routing
python3 -c 'import ast,sys; ast.parse(open(sys.argv[1]).read())' "$SOURCE_DIR/provision-routing.py"
install -o root -g root -m 0755 "$SOURCE_DIR/provision-routing.py" "$TARGET"
temporary_rule="$(mktemp)"
trap 'rm -f "$temporary_rule"' EXIT
printf 'kgh ALL=(root) NOPASSWD: /usr/local/sbin/tuf-replay-provision-routing ""\n' > "$temporary_rule"
/usr/sbin/visudo -cf "$temporary_rule"
install -o root -g root -m 0440 "$temporary_rule" "$RULE"
echo "Installed fixed, argument-free routing helper for GitHub Actions. No routes or services changed."
