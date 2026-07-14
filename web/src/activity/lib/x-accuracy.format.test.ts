import { describe, expect, test } from "bun:test";

import { formatXAccuracy } from "./x-accuracy.format";

describe("x-accuracy formatting", () => {
  test("formats the stored fraction as a percentage with two decimals", () => {
    expect(formatXAccuracy(0.98765)).toBe("98.77%");
    expect(formatXAccuracy(0)).toBe("0.00%");
  });

  test("uses a stable fallback for unavailable values", () => {
    expect(formatXAccuracy(null)).toBe("—");
    expect(formatXAccuracy(Number.NaN)).toBe("—");
  });
});
