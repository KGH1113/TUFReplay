import assert from "node:assert/strict";
import { join } from "node:path";
import { dataDir, toolBase, outgoingToken } from "./config";
import { localTuf, localTufBase } from "./local-tuf/config";
import type { Execution } from "./types";

assert(localTuf, "Run with E2E_TUF_TARGET=local and the local lab already running");
async function state() {
  return (await fetch(`${toolBase}/harness/state`)).json();
}
async function command(path: string, body?: unknown) {
  const response = await fetch(`${toolBase}/harness/${path}`, {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const value = await response.json();
  assert(response.ok, JSON.stringify(value));
  return value;
}
async function wait(id: string, expected: string) {
  const deadline = Date.now() + 90000;
  while (Date.now() < deadline) {
    const run: Execution = (await state()).executions.find((r: Execution) => r.id === id);
    assert.notEqual(run.phase, "error", run.error ?? "Execution failed");
    if (run.phase === expected) return run;
    await Bun.sleep(300);
  }
  throw new Error(`Timed out waiting for ${expected}`);
}
const initial = await state();
assert.equal(initial.tufTarget.mode, "local");
const report = [];
for (const [mode, scenario, target] of [
  ["accepted", "receipt-loss", "submitted"],
  ["unavailable", "normal", "validator_unavailable"],
] as const) {
  const issued = await command("runs", {fixtureId: initial.fixtures[0].id, mode, scenario, speed: 0});
  const saved = await wait(issued.id, "evidence_ready");
  assert.equal(saved.record?.external_pass_id, null);
  await command(`runs/${issued.id}/submit`);
  const finished = await wait(issued.id, target);
  const events = (await state()).logs.filter((event: any) => event.runId === finished.runId);
  if (mode === "accepted") {
    const pass = await (await fetch(`${localTufBase}/v2/database/passes/${finished.record!.external_pass_id}`)).json();
    assert.equal(pass.submissionSource, "auto_submission");
    assert.equal(pass.autoSubmissionRunId, finished.runId);
    assert.equal(pass.judgements.perfect, initial.fixtures[0].judgments[4]);
    assert.equal(pass.keyCount, initial.fixtures[0].keyCount);
    assert.equal(pass.videoLink, null);
    assert(events.some((e: any) => e.message.includes("응답 유실 주입")));
    const registration = events.find((e: any) => e.message === "POST /v2/internal/auto-submission/register");
    assert(registration);
    const retry = await fetch(`${localTufBase}/v2/internal/auto-submission/register`, {
      method: "POST", headers: {Authorization: `Bearer ${outgoingToken}`, "Content-Type": "application/json"},
      body: JSON.stringify(registration.detail), redirect: "error",
    });
    assert(retry.ok);
    assert.equal((await retry.json()).pass_id, pass.id, "Registration is idempotent");
  } else {
    assert.equal(finished.record?.external_pass_id, null);
    assert(!events.some((e: any) => e.message.includes("/register")));
  }
  report.push({runId: finished.runId, status: finished.phase, passId: finished.record?.external_pass_id, scenario});
  console.log(report.at(-1));
}
await Bun.write(join(dataDir, "local-tuf-verification.json"), JSON.stringify(report, null, 2));
