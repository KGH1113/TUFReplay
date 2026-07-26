import { describe, expect, test } from "bun:test";

import type { ActivityRun } from "../activity.model";
import { planLevelSessionRefresh, removeRunById } from "./use-level-session-data.hook";

describe("level session refresh planning", () => {
  test("keeps the loaded chart when only the run revision changes", () => {
    expect(planLevelSessionRefresh("level-1", true, "level-1", "level-1")).toEqual({
      levelChanged: false,
      shouldLoadChart: false,
    });
  });

  test("loads the chart for a new level or when it has not loaded yet", () => {
    expect(planLevelSessionRefresh("level-2", true, "level-1", "level-1")).toEqual({
      levelChanged: true,
      shouldLoadChart: true,
    });
    expect(planLevelSessionRefresh("level-1", true, "level-1", null)).toEqual({
      levelChanged: false,
      shouldLoadChart: true,
    });
  });

  test("removes only the deleted run without renumbering the remaining runs", () => {
    const runs = [
      { Id: "run-1", RunIndex: 3 },
      { Id: "run-2", RunIndex: 7 },
    ] as ActivityRun[];

    expect(removeRunById(runs, "run-1").map(({ Id, RunIndex }) => ({ Id, RunIndex }))).toEqual([
      { Id: "run-2", RunIndex: 7 },
    ]);
    expect(removeRunById(runs, "missing")).toEqual(runs);
  });
});
