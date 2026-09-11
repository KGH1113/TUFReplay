import { describe, expect, test } from "bun:test";

import {
  clampMicrophoneVolumeDb,
  DEFAULT_MICROPHONE_VOLUME_DB,
  formatMicrophoneVolumeDb,
  MAX_MICROPHONE_VOLUME_DB,
  microphoneDbToGain,
} from "@/models/calibration/microphone-volume";

describe("microphone volume utilities", () => {
  test("clamps decibels and falls back to unity gain", () => {
    expect(MAX_MICROPHONE_VOLUME_DB).toBe(30);
    expect(clampMicrophoneVolumeDb(34)).toBe(30);
    expect(clampMicrophoneVolumeDb(-24)).toBe(-20);
    expect(clampMicrophoneVolumeDb(Number.NaN)).toBe(DEFAULT_MICROPHONE_VOLUME_DB);
  });

  test("converts decibels to logarithmic gain and formats signed values", () => {
    expect(microphoneDbToGain(-20)).toBeCloseTo(0.1);
    expect(microphoneDbToGain(0)).toBe(1);
    expect(microphoneDbToGain(20)).toBeCloseTo(10);
    expect(microphoneDbToGain(30)).toBeCloseTo(10 ** 1.5);
    expect(formatMicrophoneVolumeDb(-8)).toBe("-8 dB");
    expect(formatMicrophoneVolumeDb(0)).toBe("0 dB");
    expect(formatMicrophoneVolumeDb(8)).toBe("+8 dB");
  });
});
