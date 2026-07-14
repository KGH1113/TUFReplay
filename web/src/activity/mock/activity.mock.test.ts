import { describe, expect, test } from "bun:test";

import { createMockActivityGateway } from "./activity.mock";

describe("activity mock", () => {
  test("serves the three downloaded TUF levels with charts and runs", async () => {
    const gateway = createMockActivityGateway();
    const sessions = await gateway.listAllAppSessions();
    const levels = sessions.flatMap((session) => session.LevelSessions);

    expect(levels.map((level) => level.TufLevelId).sort((a, b) => Number(a) - Number(b))).toEqual([5, 303, 871]);

    for (const level of levels) {
      const chart = await gateway.getChart(level.Id);
      const runs = await gateway.listAllRuns(level.Id);
      expect(chart.LevelText).toContain('"settings"');
      expect(chart.FloorCount).toBeGreaterThan(0);
      expect(runs.length).toBeGreaterThan(0);
      expect(runs.some((run) => run.StartTile > 0)).toBe(true);
    }
  });
});
