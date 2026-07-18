import { describe, expect, test } from "bun:test";

import { mockMicrophoneOffsetCalibration } from "./microphone-offset.mock";

describe("microphone offset calibration mock", () => {
  test("contains normalized deterministic waveforms for the same timeline", () => {
    const mock = mockMicrophoneOffsetCalibration;
    expect(mock.durationMs).toBe(6_000);
    expect(mock.gameWaveform.length).toBe(mock.microphoneWaveform.length);
    expect(mock.gameWaveform.length).toBeGreaterThan(100);
    for (const sample of [...mock.gameWaveform, ...mock.microphoneWaveform]) {
      expect(Number.isFinite(sample)).toBe(true);
      expect(sample).toBeGreaterThanOrEqual(0);
      expect(sample).toBeLessThanOrEqual(1);
    }
  });

  test("aligns mock microphone transients at the suggested positive delay", () => {
    const mock = mockMicrophoneOffsetCalibration;
    expect(mock.microphoneEventsMs).toEqual(
      mock.gameEventsMs.map((eventMs) => eventMs - mock.suggestedOffsetMs),
    );
  });
});
