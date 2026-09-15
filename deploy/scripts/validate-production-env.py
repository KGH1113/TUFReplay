#!/usr/bin/env python3
"""Validate the home-server env file without ever displaying its values."""

from __future__ import annotations

import re
import sys
from pathlib import Path

REQUIRED = (
    "TUF_REPLAY_DB_USER",
    "TUF_REPLAY_DB_PASSWORD",
    "TUF_TO_AUTO_SUBMISSION_TOKEN",
    "AUTO_SUBMISSION_TO_TUF_TOKEN",
    "SUBMISSION_VALIDATION_MODE",
)
IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]{0,62}\Z")
URL_SAFE_SECRET = re.compile(r"[A-Za-z0-9_-]+\Z")
VALIDATION_MODES = {"unavailable", "trusted_tester"}


def parse_env(path: Path) -> tuple[dict[str, str], set[str]]:
    values: dict[str, str] = {}
    duplicates: set[str] = set()

    for line in path.read_text(encoding="utf-8").splitlines():
        entry = line.strip()
        if not entry or entry.startswith("#"):
            continue
        if entry.startswith("export "):
            entry = entry[7:].lstrip()
        if "=" not in entry:
            continue

        name, raw_value = entry.split("=", 1)
        name = name.strip()
        if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name):
            continue

        value = raw_value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        else:
            value = re.split(r"\s+#", value, maxsplit=1)[0].rstrip()

        if name in values:
            duplicates.add(name)
        values[name] = value

    return values, duplicates


def main() -> int:
    path = Path(sys.argv[1] if len(sys.argv) > 1 else ".env.production")
    if not path.is_file():
        print("Production environment file: missing")
        print("Required file: .env.production")
        return 1

    try:
        values, duplicates = parse_env(path)
    except (OSError, UnicodeError):
        print("Production environment file: unreadable")
        return 1

    missing = [name for name in REQUIRED if not values.get(name, "").strip()]
    for name in REQUIRED:
        state = "configured" if values.get(name, "").strip() else "missing"
        print(f"{name}: {state}")

    errors: list[str] = []
    errors.extend(f"{name} appears more than once" for name in sorted(duplicates & set(REQUIRED)))
    errors.extend(f"{name} is required" for name in missing)

    db_user = values.get("TUF_REPLAY_DB_USER", "")
    if db_user and not IDENTIFIER.fullmatch(db_user):
        errors.append("TUF_REPLAY_DB_USER must be a PostgreSQL-safe identifier")

    db_password = values.get("TUF_REPLAY_DB_PASSWORD", "")
    if db_password and (
        not 32 <= len(db_password.encode("utf-8")) <= 128
        or not URL_SAFE_SECRET.fullmatch(db_password)
    ):
        errors.append("TUF_REPLAY_DB_PASSWORD must be a 32–128 byte URL-safe value")

    token_names = ("TUF_TO_AUTO_SUBMISSION_TOKEN", "AUTO_SUBMISSION_TO_TUF_TOKEN")
    for name in token_names:
        token = values.get(name, "")
        if token and (
            not 32 <= len(token.encode("utf-8")) <= 512
            or not URL_SAFE_SECRET.fullmatch(token)
        ):
            errors.append(f"{name} must be a 32–512 byte URL-safe token")
    if all(values.get(name) for name in token_names) and values[token_names[0]] == values[token_names[1]]:
        errors.append("the two internal tokens must be distinct")

    validation_mode = values.get("SUBMISSION_VALIDATION_MODE", "")
    if validation_mode and validation_mode not in VALIDATION_MODES:
        errors.append("SUBMISSION_VALIDATION_MODE must be unavailable or trusted_tester")

    if errors:
        print("Production environment preflight failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("Production environment preflight passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
