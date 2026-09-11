import { randomUUID } from "node:crypto";
import type { Execution, RunOptions } from "./types";
import { fixture } from "./fixtures/store";
import { frames } from "./client/frames";
import { request } from "./client/http";
import { Upload } from "./client/upload";
import { scenario } from "./mock/receipts";
import { log } from "./logs";

const runs: Execution[] = [];
const uploads = new Map<string, Upload>();
const busy = new Set<string>();
export function executions() {
  return runs;
}
export function execution(id: string) {
  const run = runs.find((run) => run.id === id);
  if (!run) throw new Error("Execution not found");
  return run;
}
export async function start(options: RunOptions) {
  if (
    !["accepted", "unavailable"].includes(options.mode) ||
    !["normal", "ack-loss", "receipt-loss", "gameplay-fail"].includes(options.scenario) ||
    ![0, 1, 10].includes(options.speed)
  )
    throw new Error("Invalid run options");
  if (busy.size) throw new Error("Wait for the active upload to finish");
  const item = await fixture(options.fixtureId);
  if (busy.size) throw new Error("Wait for the active upload to finish");
  const run: Execution = {
    ...options,
    id: randomUUID(),
    phase: "issuing",
    ack: -1,
    totalChunks: 0,
    sentChunks: 0,
    sentBytes: 0,
    startedAt: new Date().toISOString(),
  };
  runs.unshift(run);
  busy.add(run.id);
  void (async () => {
    const issued = await request("/api/v1/runs", "POST", {
      protocol_version: 1,
      client_game_version: String(item.meta.gameVersion ?? "e2e-recorded"),
      client_mod_version: "e2e-harness-v1",
      tuf_level_id: item.levelId,
      client_tuf_file_id: item.fileId,
      client_installed_payload_hash_hex: item.chartSha,
      client_payload_hash_version: 1,
      client_level_relative_path: "main.adofai",
    });
    run.runId = issued.run_id;
    scenario(issued.run_id, options.scenario);
    const payload = await frames(item, options.mode, issued.max_chunk_bytes);
    run.totalChunks = payload.length;
    const upload = new Upload(run, issued.upload_token, issued.heartbeat_interval_ms);
    uploads.set(run.id, upload);
    await upload.run(payload, item);
    if (run.phase === "gameplay_failed") return;
    const receipt = await request(
      `/api/v1/runs/${run.runId}/receipt`,
      "GET",
      undefined,
      issued.upload_token,
    );
    if (
      receipt.input_count !== item.inputCount ||
      receipt.hit_context_count !== item.hitCount ||
      receipt.acknowledged_sequence !== payload.length - 1
    )
      throw new Error("sealed receipt does not match fixture");
    await poll(run, new Set(["evidence_ready"]), 30000);
  })()
    .catch((error) => fail(run, error))
    .finally(() => {
      busy.delete(run.id);
      uploads.delete(run.id);
    });
  return run;
}
function fail(run: Execution, error: unknown) {
  run.phase = "error";
  run.error = String(error);
  log("tool", "실행 오류", run.error, run.runId);
}
async function poll(run: Execution, targets: Set<string>, timeout: number) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const record = await request(`/api/v1/runs/${run.runId}`);
    run.record = record;
    run.phase = record.status;
    if (targets.has(record.status)) return;
    if (
      [
        "expired",
        "validation_error",
        "registration_error",
        "validation_rejected",
        "registration_rejected",
      ].includes(record.status)
    )
      throw new Error(`${record.status}: ${record.reason}`);
    await Bun.sleep(500);
  }
  throw new Error("Server state polling timed out; inspect logs and retry after recovery");
}
export async function action(id: string, name: string) {
  const run = execution(id);
  if (name === "fail") {
    const upload = uploads.get(id);
    if (!upload) throw new Error("No active play");
    upload.fail();
    return run;
  }
  if (name === "disconnect") {
    uploads.get(id)?.disconnect();
    return run;
  }
  if (name === "reconnect") {
    uploads.get(id)?.reconnect();
    return run;
  }
  if (name !== "submit" || run.phase !== "evidence_ready")
    throw new Error("Submit requires saved evidence");
  run.phase = "submitting";
  try {
    await request(`/api/v1/runs/${run.runId}/submit`, "POST");
  } catch (error) {
    run.phase = "evidence_ready";
    throw error;
  }
  void poll(run, new Set(["submitted", "validator_unavailable"]), 180000).catch((error) =>
    fail(run, error),
  );
  return run;
}
