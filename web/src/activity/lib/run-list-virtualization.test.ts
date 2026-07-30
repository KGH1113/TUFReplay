import { describe, expect, test } from "bun:test";
import { calculateVirtualRunRange } from "./run-list-virtualization";

describe("run list virtualization", () => {
  test("renders only the viewport and overscan for a large list", () => {
    expect(calculateVirtualRunRange(10_000, 18_000, 720, 180, 4)).toEqual({
      start: 96,
      end: 109,
    });
  });

  test("excludes overscan rows from the visible animation range", () => {
    expect(calculateVirtualRunRange(10_000, 18_000, 720, 180, 0)).toEqual({
      start: 100,
      end: 105,
    });
  });

  test("clamps the range at both ends", () => {
    expect(calculateVirtualRunRange(5, 0, 360, 180, 4)).toEqual({ start: 0, end: 5 });
    expect(calculateVirtualRunRange(100, 100_000, 360, 180, 4)).toEqual({
      start: 95,
      end: 100,
    });
  });

  test("returns an empty range for an empty list", () => {
    expect(calculateVirtualRunRange(0, 0, 720, 180, 4)).toEqual({ start: 0, end: 0 });
  });
});
