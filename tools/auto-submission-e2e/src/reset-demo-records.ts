import { SQL, RedisClient } from "bun";
import { mkdir, rm } from "node:fs/promises";
import { join } from "node:path";
import { databaseUrl, redisUrl, dataDir, requireLocal } from "./config";
import { requireFreePorts } from "./ports";

await requireFreePorts([5151, 5152]);
if (
  requireLocal(databaseUrl).pathname !== "/tuf_replay_e2e" ||
  requireLocal(redisUrl).pathname !== "/14"
)
  throw new Error("Demo reset is restricted to the dedicated E2E database and Redis DB 14");

const db = new SQL(databaseUrl, { max: 1 });
const redis = new RedisClient(redisUrl);
try {
  await redis.connect();
  const before = {
    runs: (await db`SELECT count(*)::int AS count FROM run_sessions`)[0]?.count ?? 0,
    submissions:
      (await db`SELECT count(*)::int AS count FROM run_submission_records`)[0]?.count ?? 0,
    jobs: (await db`SELECT count(*)::int AS count FROM pg_loco_queue`)[0]?.count ?? 0,
  };
  const migrations = (await db`SELECT count(*)::int AS count FROM seaql_migrations`)[0]?.count ?? 0;

  await db.unsafe(
    "TRUNCATE run_submission_records,run_sessions,level_revision_charts,level_revisions,pg_loco_queue RESTART IDENTITY",
  );

  let cursor = "0";
  let redisKeysRemoved = 0;
  do {
    const [next, keys] = (await redis.send("SCAN", [
      cursor,
      "MATCH",
      "e2e:*",
      "COUNT",
      "1000",
    ])) as [string, string[]];
    cursor = next;
    if (keys.length) redisKeysRemoved += Number(await redis.send("UNLINK", keys));
  } while (cursor !== "0");

  for (const name of [
    "storage",
    "mock-tuf.sqlite",
    "mock-tuf.sqlite-wal",
    "mock-tuf.sqlite-shm",
    "lifecycle-observations.json",
    "e2e-observations.json",
    "local-tuf-verification.json",
    "reset.json",
  ])
    await rm(join(dataDir, name), { recursive: true, force: true });
  await mkdir(join(dataDir, "storage"), { recursive: true });

  const after = {
    runs: (await db`SELECT count(*)::int AS count FROM run_sessions`)[0]?.count ?? 0,
    submissions:
      (await db`SELECT count(*)::int AS count FROM run_submission_records`)[0]?.count ?? 0,
    jobs: (await db`SELECT count(*)::int AS count FROM pg_loco_queue`)[0]?.count ?? 0,
  };
  console.log(
    JSON.stringify(
      { before, after, migrationsPreserved: migrations, redisKeysRemoved, fixturesPreserved: true },
      null,
      2,
    ),
  );
} finally {
  redis.close();
  await db.close();
}
