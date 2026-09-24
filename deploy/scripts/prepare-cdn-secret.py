#!/usr/bin/env python3
"""Provision a dedicated edge signing key; stdout is only for secret-put stdin."""
import os
import pathlib
import secrets
import sys

path = pathlib.Path("/home/kgh/tuf-replay-data/auto-submission/cdn.env")
if not path.exists():
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as output:
        output.write("REPLAY_CDN_ORIGIN=https://tufreplay-cdn.impl1113.dev\n")
        output.write("REPLAY_CDN_SIGNING_SECRET=" + secrets.token_hex(32) + "\n")
if path.stat().st_mode & 0o777 != 0o600:
    raise SystemExit("CDN environment file must have mode 600")
values = dict(line.split("=", 1) for line in path.read_text().splitlines() if "=" in line)
if sys.argv[1:] == ["--print-secret"]:
    print(values["REPLAY_CDN_SIGNING_SECRET"])
else:
    print("Dedicated CDN signing configuration ready (mode 600)")
