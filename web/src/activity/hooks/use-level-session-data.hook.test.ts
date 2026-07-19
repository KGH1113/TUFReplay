import { describe, expect, test } from "bun:test";

import { planLevelSessionRefresh } from "./use-level-session-data.hook";

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
});
