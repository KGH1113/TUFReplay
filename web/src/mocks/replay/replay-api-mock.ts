import type { ReplayApi } from "@/api/replay/replay-api";
import type { ReplayLevelFilePickerResult, ReplayStatus } from "@/models/replay/replay-model";

const IDLE_STATUS: ReplayStatus = {
  operationId: null,
  runId: null,
  state: "idle",
  errorCode: null,
  message: null,
};

export function createReplayApiMock(): ReplayApi {
  let status = IDLE_STATUS;
  return {
    async play(runId) {
      status = { ...IDLE_STATUS, runId, state: "playing" };
      return status;
    },
    async getStatus() {
      return status;
    },
    async pickLevelFile(runId): Promise<ReplayLevelFilePickerResult> {
      return {
        operationId: `mock-picker-${runId}`,
        runId,
        outcome: "selected",
        levelPath: `/mock/${runId}.adofai`,
        errorCode: null,
        message: null,
      };
    },
  };
}
