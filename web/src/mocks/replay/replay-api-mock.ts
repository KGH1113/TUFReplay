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
  let startedAt = 0;
  return {
    async play(runId) {
      startedAt = Date.now();
      status = {
        ...IDLE_STATUS,
        operationId: `mock-replay-${runId}`,
        runId,
        state: "preparing",
      };
      return status;
    },
    async getStatus() {
      if (status.operationId && status.runId && status.state !== "playing") {
        const elapsed = Date.now() - startedAt;
        status = {
          ...status,
          state:
            elapsed < 700
              ? "preparing"
              : elapsed < 1_500
                ? "opening_level"
                : elapsed < 2_300
                  ? "waiting_for_focus"
                  : elapsed < 3_000
                    ? "starting"
                    : "playing",
        };
      }
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
