import { describe, expect, test } from "bun:test";
import { judgmentTimelineColumns } from "@/models/activity/judgment-timeline";

describe("judgment timeline columns", () => {
  test("places judgments by their time in the run", () => {
    const columns = judgmentTimelineColumns(
      { runId: "run", durationMs: 1_000, timesMs: [0, 500, 999], kinds: ["Early", "Late", "Miss"] },
      4,
    );
    expect(columns).toEqual(["Early", null, "Late", "Miss"]);
  });

  test("keeps the most severe judgment when several share a column", () => {
    const columns = judgmentTimelineColumns(
      {
        runId: "run",
        durationMs: 1_000,
        timesMs: [10, 20, 30, 40],
        kinds: ["EarlyPerfect", "Miss", "TooLate", "Early"],
      },
      2,
    );
    expect(columns).toEqual(["Miss", null]);
  });

  test("draws nothing for a run without a length", () => {
    expect(
      judgmentTimelineColumns({ runId: "run", durationMs: 0, timesMs: [0], kinds: ["Miss"] }, 3),
    ).toEqual([null, null, null]);
  });
});
