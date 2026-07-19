import { describe, expect, test } from "bun:test";

import {
  clampMicrophoneVolumeDb,
  DEFAULT_MICROPHONE_VOLUME_DB,
  formatMicrophoneVolumeDb,
  microphoneDbToGain,
} from "./microphone-volume.utils";

describe("microphone volume utilities", () => {
  test("clamps decibels and falls back to unity gain", () => {
    expect(clampMicrophoneVolumeDb(24)).toBe(20);
    expect(clampMicrophoneVolumeDb(-24)).toBe(-20);
    expect(clampMicrophoneVolumeDb(Number.NaN)).toBe(DEFAULT_MICROPHONE_VOLUME_DB);
  });

  test("converts decibels to logarithmic gain and formats signed values", () => {
    expect(microphoneDbToGain(-20)).toBeCloseTo(0.1);
    expect(microphoneDbToGain(0)).toBe(1);
    expect(microphoneDbToGain(20)).toBeCloseTo(10);
    expect(formatMicrophoneVolumeDb(-8)).toBe("-8 dB");
    expect(formatMicrophoneVolumeDb(0)).toBe("0 dB");
    expect(formatMicrophoneVolumeDb(8)).toBe("+8 dB");
  });
});
