import { describe, expect, test } from "bun:test";

import type { ActivityRun } from "@/models/activity/activity-model";
import { activityQueryKeys, removeRunById } from "@/state/activity/activity-queries";

describe("level session query state", () => {
  test("scopes run caches by logical level and selected app sessions", () => {
    expect(activityQueryKeys.logicalLevelRuns("level-1", ["app-1", "app-2"])).toEqual([
      "activity",
      "logical-level",
      "level-1",
      "runs",
      "app-1",
      "app-2",
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
