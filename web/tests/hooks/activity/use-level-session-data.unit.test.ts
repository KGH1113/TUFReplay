import { describe, expect, test } from "bun:test";

import type { ActivityRun } from "@/models/activity/activity-model";
import { activityQueryKeys, removeRunById } from "@/state/activity/activity-queries";

describe("level session query state", () => {
  test("scopes run caches by logical level and day", () => {
    expect(activityQueryKeys.logicalLevelRuns("level-1", "2026-10-01")).toEqual([
      "activity",
      "logical-level",
      "level-1",
      "runs",
      "2026-10-01",
    ]);
  });

  test("removes only the deleted run without renumbering the remaining runs", () => {
    const runs = [
      { id: "run-1", runIndex: 3 },
      { id: "run-2", runIndex: 7 },
    ] as ActivityRun[];

    expect(removeRunById(runs, "run-1").map(({ id, runIndex }) => ({ id, runIndex }))).toEqual([
      { id: "run-2", runIndex: 7 },
    ]);
    expect(removeRunById(runs, "missing")).toEqual(runs);
  });
});
