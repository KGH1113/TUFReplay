import type { SubmissionApi } from "@/api/submission/submission-api";
import type { SubmissionRun } from "@/models/submission/submission-model";

export function createSubmissionApiMock(): SubmissionApi {
  let connected = false;
  let disabled = false;
  let runs: SubmissionRun[] = [
    {
      cursor: 1,
      run_id: "68727984-2424-4a6d-a72b-919044143454",
      tuf_level_id: 42,
      chart_path: "main.adofai",
      status: "evidence_ready",
      reason: null,
      external_pass_id: null,
      created_at: new Date().toISOString(),
      evidence_expires_at: null,
    },
  ];
  const status = () => ({
    connected,
    configured: true,
    disabled,
    state: connected ? "ready" : "disconnected",
  });
  return {
    async connect() {
      connected = true;
      return status();
    },
    async disconnect() {
      connected = false;
      return status();
    },
    async status() {
      return status();
    },
    async setDisabled(value) {
      disabled = value;
      return status();
    },
    async list() {
      return { runs: connected ? runs.map((run) => ({ ...run })) : [], next_cursor: null };
    },
    async get(id) {
      const run = runs.find((entry) => entry.run_id === id);
      if (!run) throw new Error("Run not found");
      return { ...run };
    },
    async submit(id) {
      const run = runs.find((run) => run.run_id === id);
      if (!run) throw new Error("Run not found");
      run.status = "validator_unavailable";
      run.reason = "validator_unavailable";
      return { ...run };
    },
    async remove(id) {
      runs = runs.filter((run) => run.run_id !== id);
    },
  };
}
