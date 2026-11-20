import { describe, expect, test } from "bun:test";
import { runSpeedPercent } from "@/models/activity/run-speed";

describe("run speed", () => {
  test("shows the speed the run was played at, not the level's pitch", () => {
    // Ctrl+Space at 75% on a 100% level.
    expect(runSpeedPercent({ levelPitchPercent: 100, effectivePitch: 0.75 })).toBe(75);
    // Ctrl+Space at 75% on an 85% level.
    expect(runSpeedPercent({ levelPitchPercent: 85, effectivePitch: 0.85 * 0.75 })).toBe(63.75);
  });

  test("hides float noise from the recorded pitch", () => {
    expect(runSpeedPercent({ levelPitchPercent: 100, effectivePitch: 0.949_999_988 })).toBe(95);
  });

  test("falls back to the level's pitch without a played speed", () => {
    expect(runSpeedPercent({ levelPitchPercent: 120, effectivePitch: null })).toBe(120);
    expect(runSpeedPercent({ levelPitchPercent: 120, effectivePitch: 0 })).toBe(120);
    expect(runSpeedPercent({ levelPitchPercent: null, effectivePitch: null })).toBeNull();
  });
});
