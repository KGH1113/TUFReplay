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
  throw new Error("Reset is restricted to the dedicated E2E database and Redis DB 14");
const db = new SQL(databaseUrl, { max: 1 });
const redis = new RedisClient(redisUrl);
try {
  await redis.connect();
  const before = await db`SELECT count(*)::int AS runs FROM run_sessions`;
  const migrations = await db`SELECT count(*)::int AS count FROM seaql_migrations`;
  await db.unsafe(
    "TRUNCATE run_submission_records,run_sessions,level_revision_charts,level_revisions,pg_loco_queue RESTART IDENTITY",
  );
  let cursor = "0",
    removed = 0;
  do {
    const [next, keys] = (await redis.send("SCAN", [
      cursor,
      "MATCH",
      "e2e:*",
      "COUNT",
      "1000",
    ])) as [string, string[]];
    cursor = next;
    if (keys.length) removed += Number(await redis.send("UNLINK", keys));
  } while (cursor !== "0");
  for (const name of [
    "storage",
    "fixtures",
    "mock-tuf.sqlite",
    "mock-tuf.sqlite-wal",
    "mock-tuf.sqlite-shm",
  ])
    await rm(join(dataDir, name), { recursive: true, force: true });
  await mkdir(dataDir, { recursive: true });
  const report = {
    at: new Date().toISOString(),
    before: before[0],
    after: (await db`SELECT count(*)::int AS runs FROM run_sessions`)[0],
    migrationsPreserved: migrations[0].count,
    redisKeysRemoved: removed,
  };
  await Bun.write(join(dataDir, "reset.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
} finally {
  redis.close();
  await db.close();
}
