import { describe, expect, test } from "bun:test";

import type { ReplayStatus } from "@/models/replay/replay-model";
import { isReplayActive } from "@/state/replay/replay-queries";

const preparing: ReplayStatus = {
  operationId: "op-1",
  runId: "run-1",
  state: "preparing",
  errorCode: null,
  message: null,
};

describe("replay status polling", () => {
  test("polls only non-terminal replay states", () => {
    expect(isReplayActive(preparing)).toBe(true);
    expect(isReplayActive({ ...preparing, state: "playing" })).toBe(true);
    expect(isReplayActive({ ...preparing, state: "completed" })).toBe(false);
    expect(isReplayActive({ ...preparing, state: "cancelled" })).toBe(false);
    expect(isReplayActive({ ...preparing, state: "error" })).toBe(false);
  });
});
