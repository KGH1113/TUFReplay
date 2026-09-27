import { describe, expect, it } from "bun:test";
import { isValidFeelingRating } from "@/models/submission/feeling-rating";

describe("feeling rating", () => {
  it("accepts the TUF rating forms shown to players", () => {
    for (const rating of ["G5", "G5-G6", "G5-6", "U1", "17~18+", "Q2+", "P0"]) {
      expect(isValidFeelingRating(rating)).toBe(true);
    }
    for (const rating of ["", "G21", "G5 extra", "x".repeat(61)]) {
      expect(isValidFeelingRating(rating)).toBe(false);
    }
  });
});
