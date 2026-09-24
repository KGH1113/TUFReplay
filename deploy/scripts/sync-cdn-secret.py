#!/usr/bin/env python3
"""Send the dedicated signing key to Wrangler stdin, never logs or argv."""
import pathlib
import subprocess

root = pathlib.Path(__file__).resolve().parents[2]
result = subprocess.run(
    ["ssh", "kgh", "python3 /tmp/tuf-prepare-cdn-secret.py --print-secret"],
    stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False,
)
if result.returncode or len(result.stdout.strip()) != 64:
    raise SystemExit("Could not obtain the dedicated CDN signing key")
subprocess.run(
    ["bunx", "wrangler", "secret", "put", "SIGNING_SECRET", "--config",
     str(root / "deploy/replay-cdn/wrangler.jsonc")],
    input=result.stdout, check=True, cwd=root,
)
