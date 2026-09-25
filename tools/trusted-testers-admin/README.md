# Trusted tester administration

A small, independent operator application for managing the Rust auto-submission
server's trusted tester roster. It is not a public feature of the companion web
app. It uses Bun 1.3.11, Bun's PostgreSQL client, and no runtime dependencies.

## Ownership and database contract

The Rust server owns the PostgreSQL migration and submission eligibility query.
This application does not create or migrate tables. The migration needs to provide:

| Table | Required columns |
| --- | --- |
| `trusted_testers` | `user_id UUID PRIMARY KEY`, `label TEXT NOT NULL`, `active BOOLEAN NOT NULL`, `updated_at TIMESTAMPTZ NOT NULL`, `updated_by TEXT NOT NULL` |
| `trusted_tester_events` | `id BIGSERIAL PRIMARY KEY`, `user_id UUID NOT NULL`, `action TEXT NOT NULL` (`grant` or `revoke`), `actor TEXT NOT NULL`, `reason TEXT NOT NULL`, `created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()` |

The Rust eligibility check should require an active row for the authenticated
TUF `users.id` UUID. An absent or inactive row denies submission. Rust should
also keep an independent emergency-off switch. TUF still validates the OAuth
grant, official client, scope, linked player, account status, and final pass
registration; its environment-based tester list is removed only after Rust
enforcement is deployed and populated.

Give this app its own PostgreSQL role with only `SELECT`, `INSERT`, and `UPDATE`
on `trusted_testers`, `SELECT` and `INSERT` on `trusted_tester_events`, and
sequence usage for the events ID. It needs no `DELETE`, schema modification, or
access to run/evidence/pass tables. A grant or revoke and its audit event are
written in one transaction. Revocation retains the row and event history.

## Run locally

Set `DATABASE_URL`, `ADMIN_USERNAME` (defaults to `operator`), and a random
`ADMIN_PASSWORD` of at least 24 characters. Keep them outside Git. Then run:

```sh
bun install --frozen-lockfile
bun run start
```

The app listens on `127.0.0.1:4177` by default. Open it through an SSH port
forward over the existing Tailnet. The browser uses HTTP Basic authentication;
the password travels only through the local/Tailnet tunnel. No public Nginx or
Cloudflare route is needed. An accidental direct public bind is rejected unless
`ALLOW_NON_LOOPBACK=true` is explicitly set.

For a container, set `HOST=0.0.0.0` and `ALLOW_NON_LOOPBACK=true` inside the
container, but publish the container port on **host loopback only** (for
example `127.0.0.1:4177:4177`). The main deployment owns that Compose wiring,
the database role, and the migration. Never expose the container port on all
host interfaces.

The UI lists up to 500 recently changed testers and 100 recent events. To add a
tester, enter a numeric TUF player ID and look up its linked account through
`https://api.tuforums.com/v2/database/players/{id}`. The app displays the account
name and UUID for confirmation, then resolves the player again on submission.
It stores only the linked TUF user UUID; a player without a linked account cannot
be granted access. Adding an existing UUID reactivates it and records another
`grant` event. Disabling an
active tester records a `revoke` event; it does not delete existing passes.
Every mutation requires a same-origin JSON request, a reason, and administrator
credentials. All served content uses a restrictive Content Security Policy and
`no-store` cache headers.

## Check

```sh
bun run test
bun run typecheck
```

The tests use an in-memory repository and do not connect to production data.
Integration with the real PostgreSQL migration and Rust eligibility path is
owned by the main release task.
