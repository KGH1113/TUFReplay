import { describe, expect, test } from "bun:test";
import { recordingTimeRemaining } from "@/models/activity/run-recordings";

describe("recording retention labels", () => {
  const now = Date.parse("2026-10-15T00:00:00Z");
  test("expired and missing deadlines have no remaining time", () => {
    for (const value of [null, "invalid", "2026-10-14T00:00:00Z", "2026-10-15T00:00:00Z"])
      expect(recordingTimeRemaining(value, now)).toEqual({ unit: "soon", count: 0 });
  });
  test("rounds up partial hours and switches to days at 24 hours", () => {
    expect(recordingTimeRemaining("2026-10-15T00:00:01Z", now)).toEqual({
      unit: "hours",
      count: 1,
    });
    expect(recordingTimeRemaining("2026-10-15T22:59:59Z", now)).toEqual({
      unit: "hours",
      count: 23,
    });
    expect(recordingTimeRemaining("2026-10-16T00:00:00Z", now)).toEqual({ unit: "days", count: 1 });
    expect(recordingTimeRemaining("2026-10-16T00:00:01Z", now)).toEqual({ unit: "days", count: 2 });
  });
});
