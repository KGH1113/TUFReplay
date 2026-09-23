import { SQL } from "bun";

/** Seeds only a local test account, preserving an operator's later revocation. */
export async function seedTrustedTester(databaseUrl: string, userId: string) {
  const url = new URL(databaseUrl);
  if (!["127.0.0.1", "localhost", "[::1]"].includes(url.hostname)
    || !["/tuf_replay_e2e", "/tuf_replay_local_game"].includes(url.pathname)) {
    throw new Error("Tester fixtures require the isolated local E2E database");
  }
  const db = new SQL(databaseUrl, { max: 1 });
  try {
    await db.begin(async tx => {
      const inserted = await tx`
        INSERT INTO trusted_testers(user_id,label,active,updated_at,updated_by)
        VALUES (${userId},'Local E2E tester',TRUE,NOW(),'local-e2e')
        ON CONFLICT(user_id) DO NOTHING RETURNING user_id
      `;
      if (inserted.length) await tx`
        INSERT INTO trusted_tester_events(user_id,action,actor,reason)
        VALUES (${userId},'grant','local-e2e','Initialize isolated test account')
      `;
    });
  } finally { await db.close(); }
}
