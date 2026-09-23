"""Render a one-time, operator-provided UUID roster as atomic PostgreSQL SQL."""

import pathlib
import sys
import uuid


def render(roster: str) -> str:
    users = sorted({str(uuid.UUID(line.strip())) for line in roster.splitlines() if line.strip()})
    if not users:
        raise ValueError("Initial tester roster must not be empty")
    values = ",".join(f"('{user}'::uuid)" for user in users)
    return f"""BEGIN;
WITH inserted AS (
  INSERT INTO trusted_testers (user_id, label, active, updated_at, updated_by)
  SELECT user_id, 'Migrated tester', TRUE, NOW(), 'deployment-bootstrap'
  FROM (VALUES {values}) AS roster(user_id)
  ON CONFLICT (user_id) DO NOTHING
  RETURNING user_id
)
INSERT INTO trusted_tester_events (user_id, action, actor, reason)
SELECT user_id, 'grant', 'deployment-bootstrap', 'Migrated existing TUF tester allowlist'
FROM inserted;
COMMIT;
"""


if __name__ == "__main__":
    try:
        print(render(pathlib.Path(sys.argv[1]).read_text()), end="")
    except (ValueError, OSError):
        sys.exit("Invalid initial tester roster; no SQL generated")
