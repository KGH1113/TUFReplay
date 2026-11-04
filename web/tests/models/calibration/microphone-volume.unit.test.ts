import { describe, expect, test } from "bun:test";

import {
  clampMicrophoneVolumeDb,
  DEFAULT_MICROPHONE_VOLUME_DB,
  formatMicrophoneVolumeDb,
  MAX_MICROPHONE_VOLUME_DB,
  MICROPHONE_VOLUME_STEP_DB,
  MIN_MICROPHONE_VOLUME_DB,
  microphoneDbToGain,
} from "@/models/calibration/microphone-volume";

describe("microphone volume utilities", () => {
  test("uses OBS Gain filter range and step", () => {
    expect(MIN_MICROPHONE_VOLUME_DB).toBe(-30);
    expect(MAX_MICROPHONE_VOLUME_DB).toBe(30);
    expect(MICROPHONE_VOLUME_STEP_DB).toBe(0.1);
  });

  test("clamps, rounds to 0.1 dB, and falls back to unity gain", () => {
    expect(clampMicrophoneVolumeDb(34)).toBe(30);
    expect(clampMicrophoneVolumeDb(-34)).toBe(-30);
    expect(clampMicrophoneVolumeDb(3.46)).toBe(3.5);
    expect(clampMicrophoneVolumeDb(-12.34)).toBe(-12.3);
    expect(clampMicrophoneVolumeDb(0.3)).toBe(0.3);
    expect(Object.is(clampMicrophoneVolumeDb(-0.04), 0)).toBe(true);
    expect(clampMicrophoneVolumeDb(Number.POSITIVE_INFINITY)).toBe(30);
    expect(clampMicrophoneVolumeDb(Number.NaN)).toBe(DEFAULT_MICROPHONE_VOLUME_DB);
  });

  test("converts decibels like OBS db_to_mul", () => {
    expect(microphoneDbToGain(-30)).toBeCloseTo(0.0316228, 6);
    expect(microphoneDbToGain(-20)).toBeCloseTo(0.1, 6);
    expect(microphoneDbToGain(0)).toBe(1);
    expect(microphoneDbToGain(0.1)).toBeCloseTo(1.0115794, 6);
    expect(microphoneDbToGain(20)).toBeCloseTo(10, 5);
    expect(microphoneDbToGain(30)).toBeCloseTo(10 ** 1.5, 5);
  });

  test("formats signed values with one decimal", () => {
    expect(formatMicrophoneVolumeDb(-12.3)).toBe("-12.3 dB");
    expect(formatMicrophoneVolumeDb(0)).toBe("0.0 dB");
    expect(formatMicrophoneVolumeDb(-0.04)).toBe("0.0 dB");
    expect(formatMicrophoneVolumeDb(3.5)).toBe("+3.5 dB");
    expect(formatMicrophoneVolumeDb(30)).toBe("+30.0 dB");
  });
});
