import { describe, expect, test } from "bun:test";
import { createReplayApi } from "@/api/replay/create-replay-api";
import { scriptedChannels } from "../../fixtures/local-message-peer";

describe("level selection for render", () => {
  test("requests screen restoration only for rendering and keeps replay selection compatible", async () => {
    const calls: unknown[] = [];
    const api = createReplayApi(
      scriptedChannels(async (method: string, params: unknown) => {
        calls.push({ method, params });
        return {
          OperationId: "pick-1",
          RunId: "run-1",
          Outcome: "selected",
          LevelPath: "/levels/matching.adofai",
          ErrorCode: null,
          Message: null,
        };
      }),
    );
    expect((await api.pickLevelFile("run-1", "render")).levelPath).toBe("/levels/matching.adofai");
    await api.pickLevelFile("run-1");
    expect(calls).toEqual([
      { method: "replay.level-file.choose", params: { runId: "run-1", purpose: "render" } },
      { method: "replay.level-file.choose", params: { runId: "run-1" } },
    ]);
  });

  test("keeps mismatch and cancelled selections from becoming a chosen path", async () => {
    for (const outcome of ["mismatch", "cancelled"] as const) {
      const api = createReplayApi(
        scriptedChannels(async () => ({
          OperationId: "pick-1",
          RunId: "run-1",
          Outcome: outcome,
          LevelPath: outcome === "mismatch" ? "/levels/wrong.adofai" : null,
          ErrorCode: outcome === "mismatch" ? "level_gameplay_modified" : null,
          Message: null,
        })),
      );
      expect((await api.pickLevelFile("run-1", "render")).outcome).toBe(outcome);
    }
  });
});
