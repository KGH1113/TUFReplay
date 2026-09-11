# Local demo clean state

Verified on 2026-09-10 before the demo.

## TUF local services

- API: `http://127.0.0.1:3002`
- Frontend: `http://127.0.0.1:5176`
- `passes`: 0
- `judgements`: 0
- `auto_submission_receipts`: 0
- `player_pass_summary`: 0
- Levels with a nonzero `clears` value: 0
- Elasticsearch pass documents: 0
- Elasticsearch levels with nonzero clear statistics: 0
- Elasticsearch players with nonzero pass/score statistics: 0
- Level 3072 API response: `clears=0`, `uniqueClears=0`

Levels, difficulties, accounts, OAuth configuration, and imported metadata were preserved.

## Auto-submission E2E services

- API/worker/scheduler: `http://127.0.0.1:5151`
- Runner and mock integration service: `http://127.0.0.1:5152`
- Test UI: `http://127.0.0.1:5175`
- `run_sessions`: 0
- `run_submission_records`: 0
- `pg_loco_queue`: 0
- Redis DB 14 keys: 0
- E2E storage and mock receipts: empty

Fixtures, schema migrations, levels, users, and the local OAuth setup were preserved. Opening the UI does not create a run; pressing the play/upload controls will create new demo records.

## Reusable cleanup scripts

- `tools/auto-submission-e2e/src/reset-demo-records.ts`
- `tools/auto-submission-e2e/src/local-tuf/reset-demo-submissions.ts`

Both scripts contain local-only endpoint and database guards. The auto-submission reset must run while ports 5151 and 5152 are stopped.
