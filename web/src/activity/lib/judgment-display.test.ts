import { describe, expect, test } from "bun:test";

import { judgmentDisplayItems } from "./judgment-display";

describe("judgment display", () => {
  test("keeps the ADOFAI/Jipper order and colors", () => {
    const items = judgmentDisplayItems({ Overload: 9, Perfect: 42, Miss: 3 });

    expect(items.map(({ key }) => key)).toEqual([
      "Overload",
      "TooEarly",
      "Early",
      "EarlyPerfect",
      "Perfect",
      "LatePerfect",
      "Late",
      "TooLate",
      "Miss",
    ]);
    expect(items.map(({ color }) => color)).toEqual([
      "#D958FF",
      "#FF0000",
      "#FF6F4E",
      "#A0FF4E",
      "#60FF4E",
      "#A0FF4E",
      "#FF6F4E",
      "#FF0000",
      "#D958FF",
    ]);
    expect(items.map(({ value }) => value)).toEqual([9, 0, 0, 0, 42, 0, 0, 0, 3]);
  });

  test("falls back safely for missing or invalid counts", () => {
    expect(judgmentDisplayItems(undefined).every(({ value }) => value === 0)).toBe(true);
    expect(
      judgmentDisplayItems({ Early: -2, Late: Number.NaN }).find(({ key }) => key === "Early")
        ?.value,
    ).toBe(0);
  });
});
