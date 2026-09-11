import { SQL, RedisClient } from "bun";
import { databaseUrl, redisUrl, requireLocal } from "./config";
import { fixtures } from "./fixtures/store";

export async function preflight() {
  const dbUrl = requireLocal(databaseUrl),
    redis = requireLocal(redisUrl);
  if (dbUrl.pathname !== "/tuf_replay_e2e" || redis.pathname !== "/14")
    throw new Error("Use the dedicated tuf_replay_e2e PostgreSQL database and Redis DB 14");
  const db = new SQL(databaseUrl, { max: 1 });
  try {
    await db`SELECT 1`;
  } catch {
    throw new Error(
      "E2E PostgreSQL 연결 실패. tuf_replay_e2e DB를 생성하거나 E2E_DATABASE_URL을 설정하세요. README 참고.",
    );
  } finally {
    await db.close();
  }
  const client = new RedisClient(redisUrl);
  try {
    await client.connect();
    await client.send("PING", []);
  } catch {
    throw new Error(
      "E2E Redis 연결 실패. 로컬 Redis를 실행하거나 E2E_REDIS_URL을 설정하세요 (DB 14).",
    );
  } finally {
    client.close();
  }
  if (!(await fixtures()).length) throw new Error("먼저 bun run e2e:prepare를 실행하세요.");
}
