import { describe, expect, test } from "bun:test";

import { formatXAccuracy } from "./x-accuracy.format";

describe("x-accuracy formatting", () => {
  test("formats the stored fraction as a percentage with two decimals", () => {
    expect(formatXAccuracy(0.98765, "en-US")).toBe("98.77%");
    expect(formatXAccuracy(0, "en-US")).toBe("0.00%");
  });

  test("uses a stable fallback for unavailable values", () => {
    expect(formatXAccuracy(null, "en-US")).toBe("—");
    expect(formatXAccuracy(Number.NaN, "en-US")).toBe("—");
  });
});
