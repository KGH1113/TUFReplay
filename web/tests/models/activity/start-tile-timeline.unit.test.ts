import { describe, expect, test } from "bun:test";
import { layoutTimelineMarkers } from "@/models/activity/start-tile-timeline";

describe("start tile timeline layout", () => {
  test("places markers by their position inside the track", () => {
    expect(layoutTimelineMarkers([0, 0.5, 1], 224, 24, 28)).toEqual([12, 112, 212]);
  });

  test("separates markers that would overlap and keeps their order", () => {
    const centers = layoutTimelineMarkers([0.1, 0.11, 0.12], 424, 24, 28);
    expect(centers[1] - centers[0]).toBeGreaterThanOrEqual(28);
    expect(centers[2] - centers[1]).toBeGreaterThanOrEqual(28);
  });

  test("pulls markers crowded at the end back inside the track", () => {
    const centers = layoutTimelineMarkers([0.98, 0.99, 1], 224, 24, 28);
    expect(centers.at(-1)).toBe(212);
    expect(centers[1]).toBe(184);
    expect(centers[0]).toBe(156);
  });
});
