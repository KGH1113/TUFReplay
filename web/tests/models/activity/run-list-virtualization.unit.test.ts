import { describe, expect, test } from "bun:test";
import {
  anchoredRunScrollTop,
  calculateVirtualRunRange,
} from "@/models/activity/run-list-virtualization";

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

describe("run list scroll anchoring", () => {
  const rows = (...ids: string[]) => ids.map((id) => ({ id }));
  const original = rows("a", "b", "c", "d", "e");

  test("keeps the visible card at its pixel offset when new runs sort ahead of it", () => {
    expect(
      anchoredRunScrollTop(
        original,
        rows("new-2", "new-1", "a", "b", "c", "d", "e"),
        370,
        180,
        180,
      ),
    ).toBe(730);
  });

  test("shows new runs immediately when the user is at the top", () => {
    expect(anchoredRunScrollTop(original, rows("new", "a", "b", "c", "d", "e"), 0, 180, 180)).toBe(
      0,
    );
  });

  test("does not move the view when a run is inserted below it", () => {
    expect(
      anchoredRunScrollTop(original, rows("a", "b", "c", "d", "new", "e"), 370, 180, 180),
    ).toBe(370);
  });

  test("keeps the visible card in place when earlier runs are deleted", () => {
    expect(anchoredRunScrollTop(original, rows("a", "c", "d", "e"), 370, 180, 180)).toBe(190);
  });

  test("anchors a surviving neighbor if the visible run is deleted", () => {
    expect(anchoredRunScrollTop(original, rows("a", "b", "d", "e"), 370, 180, 180)).toBe(190);
  });

  test("preserves the card's clipped offset after a row height change", () => {
    expect(anchoredRunScrollTop(original, original, 410, 200, 240)).toBe(490);
  });

  test("starts at the top for an unrelated or empty list", () => {
    expect(anchoredRunScrollTop(original, rows("x", "y"), 370, 180, 180)).toBe(0);
    expect(anchoredRunScrollTop(original, [], 370, 180, 180)).toBe(0);
  });
});
