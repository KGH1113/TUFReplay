import { SQL, RedisClient } from "bun";
import { join } from "node:path";
import { databaseUrl, redisUrl, dataDir } from "./config";

export class Inspector {
  private db = new SQL(databaseUrl, { max: 1 });
  readonly redis = new RedisClient(redisUrl);
  readonly observations: unknown[] = [];
  async snapshot(stage: string, runId: string) {
    const [row] = await this
      .db`SELECT r.pid,r.status,s.state,s.manifest,s.ingest_released_at,s.external_pass_id,s.retry_count
      FROM run_sessions r JOIN run_submission_records s ON s.run_session_id=r.id WHERE r.pid=${runId}`;
    const key = `e2e:tufreplay:run:${runId}`;
    const streams = row.manifest?.streams ?? [];
    const files = await Promise.all(
      streams.map(async (stream: { storage_key: string; bytes: number }) => ({
        key: stream.storage_key,
        expectedBytes: stream.bytes,
        exists: await Bun.file(join(dataDir, "storage", stream.storage_key)).exists(),
      })),
    );
    const value = {
      stage,
      at: new Date().toISOString(),
      runId,
      status: row.status,
      state: row.state,
      hasManifest: row.manifest !== null,
      releasedAt: row.ingest_released_at,
      passId: row.external_pass_id,
      retryCount: row.retry_count,
      files,
      chunks: Number(await this.redis.send("XLEN", [`${key}:chunks`])),
      chunkTtl: Number(await this.redis.send("TTL", [`${key}:chunks`])),
      chunkMemory: await this.redis.send("MEMORY", ["USAGE", `${key}:chunks`]),
      metaTtl: Number(await this.redis.send("TTL", [`${key}:meta`])),
    };
    this.observations.push(value);
    return value;
  }
  async waitFor(
    runId: string,
    predicate: (row: { state: string; ingest_released_at: unknown }) => boolean,
  ) {
    const deadline = Date.now() + 30000;
    while (Date.now() < deadline) {
      const [row] = await this
        .db`SELECT s.state,s.ingest_released_at FROM run_submission_records s JOIN run_sessions r ON r.id=s.run_session_id WHERE r.pid=${runId}`;
      if (predicate(row)) return;
      await Bun.sleep(100);
    }
    throw new Error("Timed out waiting for inspected DB state");
  }
  async save(name = "lifecycle-observations.json") {
    await Bun.write(join(dataDir, name), JSON.stringify(this.observations, null, 2));
  }
  async close() {
    this.redis.close();
    await this.db.close();
  }
}
