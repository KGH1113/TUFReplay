import assert from "node:assert/strict";
import { preflight } from "./preflight";
import { serve } from "./service";
import { rustServer, ready } from "./processes";
import { toolBase, dataDir, databaseUrl } from "./config";
import { fixtures, fixturePath } from "./fixtures/store";
import { SQL } from "bun";
import { join } from "node:path";
import type { Execution, RunOptions } from "./types";
import { lookup, registerAttempts } from "./mock/receipts";
import { requireFreePorts } from "./ports";
import { Inspector } from "./inspection";

await preflight();
await requireFreePorts([5151, 5152]);
const service = serve();
const rust = rustServer();
const db = new SQL(databaseUrl, { max: 1 });
const inspector = new Inspector();
async function post(path: string, body?: unknown) {
  const response = await fetch(`${toolBase}/harness/${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });
  const value = await response.json();
  assert.ok(response.ok, JSON.stringify(value));
  return value;
}
async function wait(id: string, phase: string): Promise<Execution> {
  const deadline = Date.now() + 60000;
  while (Date.now() < deadline) {
    const state = await (await fetch(`${toolBase}/harness/state`)).json();
    const run = state.executions.find((r: Execution) => r.id === id);
    assert.notEqual(run?.phase, "error", run?.error);
    if (run?.phase === phase) return run;
    await Bun.sleep(100);
  }
  throw new Error(`Timed out waiting for ${phase}`);
}
try {
  await inspector.redis.connect();
  await ready(rust);
  const items = await fixtures();
  const cases: RunOptions[] = [
    ...items.map((item) => ({
      fixtureId: item.id,
      mode: "accepted" as const,
      speed: 0 as const,
      scenario: "normal" as const,
    })),
    {
      fixtureId: items[0].id,
      mode: "unavailable",
      speed: 0,
      scenario: "normal",
    },
    {
      fixtureId: items[0].id,
      mode: "accepted",
      speed: 0,
      scenario: "ack-loss",
    },
    {
      fixtureId: items[0].id,
      mode: "accepted",
      speed: 0,
      scenario: "receipt-loss",
    },
  ];
  for (const options of cases) {
    const created = await post("runs", options);
    const saved = await wait(created.id, "evidence_ready");
    await inspector.waitFor(saved.runId!, (row) => row.ingest_released_at !== null);
    const storedState = await inspector.snapshot(
      `saved-${options.mode}-${options.scenario}`,
      saved.runId!,
    );
    assert.equal(storedState.chunkTtl, -2);
    assert.ok(storedState.metaTtl > 0);
    assert.equal(lookup(saved.runId!), null, "no registration before Submit");
    const [record] =
      await db`SELECT s.manifest FROM run_submission_records s JOIN run_sessions r ON r.id=s.run_session_id WHERE r.pid=${saved.runId}`;
    const item = items.find((f) => f.id === options.fixtureId)!;
    for (const [kind, file] of [
      [0, "inputs.csv"],
      [1, "hits.csv"],
    ] as const) {
      const stream = record.manifest.streams.find((s: any) => s.kind === kind);
      const stored = await Bun.file(join(dataDir, "storage", stream.storage_key)).text();
      assert.equal(
        stored,
        await Bun.file(fixturePath(item, file)).text(),
        "stored evidence exactly matches original trimmed stream",
      );
    }
    await post(`runs/${created.id}/submit`);
    const finished = await wait(
      created.id,
      options.mode === "accepted" ? "submitted" : "validator_unavailable",
    );
    assert.equal(Boolean(lookup(finished.runId!)), options.mode === "accepted");
    const terminal = await inspector.snapshot(
      `finished-${options.mode}-${options.scenario}`,
      finished.runId!,
    );
    assert.equal(terminal.chunkTtl, -2);
    assert.equal(
      registerAttempts(finished.runId!),
      options.mode === "accepted" ? 1 : 0,
      "Registration response loss must recover by receipt lookup; count persisted calls, not the bounded UI log buffer",
    );
    console.log(`PASS ${item.song} · ${options.mode} · ${options.scenario}`);
  }
  for (const manual of [false, true]) {
    const created = await post("runs", {
      fixtureId: items[0].id,
      mode: "accepted",
      speed: manual ? 1 : 0,
      scenario: manual ? "normal" : "gameplay-fail",
    });
    if (manual) {
      await wait(created.id, "uploading");
      await post(`runs/${created.id}/fail`);
    }
    const failed = await wait(created.id, "gameplay_failed");
    const observed = await inspector.snapshot(
      manual ? "manual-failure" : "40-percent-failure",
      failed.runId!,
    );
    assert.equal(observed.status, "failed");
    assert.equal(observed.hasManifest, false);
    assert.equal(observed.chunkTtl, -2);
    assert.equal(observed.metaTtl, -2);
    assert.equal(registerAttempts(failed.runId!), 0);
    const submit = await fetch(`${toolBase}/harness/runs/${failed.id}/submit`, { method: "POST" });
    assert.equal(submit.status, 400);
    if (!manual) assert.ok((failed.playTimeUs ?? 0) < items[0].durationUs * 0.4);
    console.log(
      `PASS ${manual ? "manual" : "40 percent"} gameplay failure: no evidence, no Redis keys, no registration`,
    );
  }
  console.log(`E2E: ${cases.length + 2} real HTTP/WS scenarios passed`);
} finally {
  rust.kill("SIGTERM");
  await service.stop(true);
  await db.close();
  await rust.exited;
  await inspector.save("e2e-observations.json");
  await inspector.close();
}
