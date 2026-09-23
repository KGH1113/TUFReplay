import { expect, test } from "bun:test";
import { SQL } from "bun";
import { createTesterRepository } from "../trusted-testers-admin/src/testers.repository";

const url = process.env.TESTER_ADMIN_TEST_DATABASE_URL;
test.skipIf(!url)("admin repository writes membership and audit events atomically in PostgreSQL", async () => {
  const destination = new URL(url!);
  if (!["127.0.0.1", "localhost", "[::1]"].includes(destination.hostname)
    || destination.pathname !== "/tuf_replay_test") throw new Error("Requires the isolated local test database");
  const db = new SQL(url!, { max: 1 });
  const repository = createTesterRepository(url!);
  const id = crypto.randomUUID();
  try {
    expect((await repository.grant(id, "테스터", "integration", "grant test")).active).toBe(true);
    expect((await repository.revoke(id, "integration", "revoke test"))?.active).toBe(false);
    expect(await repository.revoke(id, "integration", "duplicate")).toBeNull();
    expect((await repository.grant(id, "다시 활성화", "integration", "reactivate")).active).toBe(true);
    const events = await db`SELECT action, reason FROM trusted_tester_events WHERE user_id=${id} ORDER BY id`;
    expect(events.map(row => row.action)).toEqual(["grant", "revoke", "grant"]);
    // A failed event insert must roll back its membership mutation too.
    await expect(repository.grant(id, "must roll back", "integration", null as unknown as string)).rejects.toThrow();
    const rows = await db`SELECT label,active FROM trusted_testers WHERE user_id=${id}`;
    expect(rows[0].label).toBe("다시 활성화");
    expect(rows[0].active).toBe(true);
  } finally {
    await repository.close();
    await db`DELETE FROM trusted_tester_events WHERE user_id=${id}`;
    await db`DELETE FROM trusted_testers WHERE user_id=${id}`;
    await db.close();
  }
});
