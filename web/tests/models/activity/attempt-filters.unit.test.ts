import { describe, expect, test } from "bun:test";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  activeAttemptFilterCount,
  applyAttemptFilters,
  attemptFilterOptions,
  noAttemptFilters,
} from "@/models/activity/attempt-filters";

function run(overrides: Partial<ActivityRun>): ActivityRun {
  return {
    id: "run",
    result: "Failed",
    startTile: 0,
    noFailMode: false,
    locked: false,
    hasMicrophoneRecording: false,
    hasCameraRecording: false,
    levelPitchPercent: 100,
    judgmentDifficulty: "Normal",
    ...overrides,
  } as ActivityRun;
}

describe("attempt filters", () => {
  const runs = [
    run({ id: "clear", result: "Cleared", locked: true, hasCameraRecording: true }),
    run({ id: "checkpoint", result: "Cleared", startTile: 120, levelPitchPercent: 110 }),
    run({ id: "no-fail", result: "Cleared", noFailMode: true, judgmentDifficulty: "Strict" }),
    run({ id: "fail", hasMicrophoneRecording: true }),
  ];

  test("keeps every attempt without active filters", () => {
    expect(applyAttemptFilters(runs, noAttemptFilters)).toBe(runs);
    expect(activeAttemptFilterCount(noAttemptFilters)).toBe(0);
  });

  test("counts only submittable clears as clears", () => {
    const filtered = applyAttemptFilters(runs, { ...noAttemptFilters, clearsOnly: true });
    expect(filtered.map((item) => item.id)).toEqual(["clear"]);
  });

  test("combines filters", () => {
    const filters = { ...noAttemptFilters, withRecordings: true, hideNoFail: true, pitches: [100] };
    expect(applyAttemptFilters(runs, filters).map((item) => item.id)).toEqual(["clear", "fail"]);
    expect(activeAttemptFilterCount(filters)).toBe(3);
  });

  test("offers pitch and difficulty choices only when attempts differ", () => {
    expect(attemptFilterOptions(runs)).toEqual({
      pitches: [100, 110],
      difficulties: ["Normal", "Strict"],
    });
    expect(attemptFilterOptions([runs[0]])).toEqual({ pitches: [], difficulties: [] });
  });
});
