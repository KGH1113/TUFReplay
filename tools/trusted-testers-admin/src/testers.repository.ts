import { SQL } from "bun";
import type { Tester, TesterEvent, TesterRepository } from "./testers.model";

type TesterRow = {
  user_id: string;
  label: string;
  active: boolean;
  updated_at: Date | string;
  updated_by: string;
};
type EventRow = {
  id: string | number | bigint;
  user_id: string;
  action: "grant" | "revoke";
  actor: string;
  reason: string;
  created_at: Date | string;
};

const instant = (value: Date | string) => new Date(value).toISOString();
const tester = (row: TesterRow): Tester => ({
  userId: row.user_id,
  label: row.label,
  active: row.active,
  updatedAt: instant(row.updated_at),
  updatedBy: row.updated_by,
});
const event = (row: EventRow): TesterEvent => ({
  id: String(row.id),
  userId: row.user_id,
  action: row.action,
  actor: row.actor,
  reason: row.reason,
  createdAt: instant(row.created_at),
});

/** Uses the schema owned by the Rust server; this app does not run migrations. */
export function createTesterRepository(databaseUrl: string): TesterRepository & { close(): Promise<void> } {
  const db = new SQL(databaseUrl, { max: 3 });
  return {
    async list() {
      const [testers, events] = await Promise.all([
        db<TesterRow[]>`
          SELECT user_id, label, active, updated_at, updated_by
          FROM trusted_testers ORDER BY updated_at DESC LIMIT 500
        `,
        db<EventRow[]>`
          SELECT id, user_id, action, actor, reason, created_at
          FROM trusted_tester_events ORDER BY id DESC LIMIT 100
        `,
      ]);
      return { testers: testers.map(tester), events: events.map(event) };
    },
    async grant(userId, label, actor, reason) {
      return db.begin(async (tx) => {
        const rows = await tx<TesterRow[]>`
          INSERT INTO trusted_testers (user_id, label, active, updated_at, updated_by)
          VALUES (${userId}, ${label}, TRUE, NOW(), ${actor})
          ON CONFLICT (user_id) DO UPDATE SET
            label = EXCLUDED.label,
            active = TRUE,
            updated_at = NOW(),
            updated_by = EXCLUDED.updated_by
          RETURNING user_id, label, active, updated_at, updated_by
        `;
        await tx`
          INSERT INTO trusted_tester_events (user_id, action, actor, reason)
          VALUES (${userId}, 'grant', ${actor}, ${reason})
        `;
        return tester(rows[0]!);
      });
    },
    async revoke(userId, actor, reason) {
      return db.begin(async (tx) => {
        const rows = await tx<TesterRow[]>`
          UPDATE trusted_testers
          SET active = FALSE, updated_at = NOW(), updated_by = ${actor}
          WHERE user_id = ${userId} AND active = TRUE
          RETURNING user_id, label, active, updated_at, updated_by
        `;
        if (!rows[0]) return null;
        await tx`
          INSERT INTO trusted_tester_events (user_id, action, actor, reason)
          VALUES (${userId}, 'revoke', ${actor}, ${reason})
        `;
        return tester(rows[0]);
      });
    },
    async close() {
      await db.close();
    },
  };
}
