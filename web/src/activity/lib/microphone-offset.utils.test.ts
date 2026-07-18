import { describe, expect, test } from "bun:test";

import {
  buildWaveformAreaPath,
  clampMicrophoneOffset,
  formatMicrophoneOffset,
  keyboardOffsetAdjustment,
  offsetFromPointerDelta,
} from "./microphone-offset.utils";

describe("microphone offset utilities", () => {
  test("clamps and rounds offsets to the supported millisecond range", () => {
    expect(clampMicrophoneOffset(-800)).toBe(-500);
    expect(clampMicrophoneOffset(124.6)).toBe(125);
    expect(clampMicrophoneOffset(800)).toBe(500);
  });

  test("maps horizontal pointer movement onto the shared timeline", () => {
    expect(offsetFromPointerDelta(0, 16, 1_000, 6_000)).toBe(96);
    expect(offsetFromPointerDelta(490, 100, 1_000, 6_000)).toBe(500);
    expect(offsetFromPointerDelta(42, 10, 0, 6_000)).toBe(42);
  });

  test("supports precise and accelerated keyboard adjustment", () => {
    expect(keyboardOffsetAdjustment(0, "ArrowRight", false)).toBe(1);
    expect(keyboardOffsetAdjustment(0, "ArrowLeft", true)).toBe(-10);
    expect(keyboardOffsetAdjustment(0, "Enter", false)).toBeNull();
  });

  test("formats signed values and creates a closed waveform path", () => {
    expect(formatMicrophoneOffset(-12)).toBe("−12 ms");
    expect(formatMicrophoneOffset(0)).toBe("0 ms");
    expect(formatMicrophoneOffset(96)).toBe("+96 ms");
    expect(buildWaveformAreaPath([0, 0.5, 1])).toMatch(/^M.+ Z$/);
  });
});
