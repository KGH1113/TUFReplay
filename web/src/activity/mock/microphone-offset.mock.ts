import type { MicrophoneOffsetCalibrationData } from "../activity.model";

export interface MockMicrophoneOffsetCalibrationData extends MicrophoneOffsetCalibrationData {
  durationMs: number;
  initialOffsetMs: number;
  suggestedOffsetMs: number;
  gameEventsMs: number[];
  microphoneEventsMs: number[];
  gameWaveform: number[];
  microphoneWaveform: number[];
}

const DURATION_MS = 6_000;
const SUGGESTED_OFFSET_MS = 96;
const WAVEFORM_SAMPLE_COUNT = 480;
const GAME_EVENTS_MS = [520, 1_180, 1_920, 2_760, 3_420, 4_260, 5_140];
const MICROPHONE_EVENTS_MS = GAME_EVENTS_MS.map((eventMs) => eventMs - SUGGESTED_OFFSET_MS);

export const mockMicrophoneOffsetCalibration: MockMicrophoneOffsetCalibrationData = {
  durationMs: DURATION_MS,
  initialOffsetMs: 0,
  suggestedOffsetMs: SUGGESTED_OFFSET_MS,
  gameEventsMs: GAME_EVENTS_MS,
  microphoneEventsMs: MICROPHONE_EVENTS_MS,
  gameWaveform: createWaveform(GAME_EVENTS_MS, "game"),
  microphoneWaveform: createWaveform(MICROPHONE_EVENTS_MS, "microphone"),
};

function createWaveform(eventsMs: number[], kind: "game" | "microphone") {
  return Array.from({ length: WAVEFORM_SAMPLE_COUNT }, (_, index) => {
    const timeMs = (index / (WAVEFORM_SAMPLE_COUNT - 1)) * DURATION_MS;
    const eventEnergy = eventsMs.reduce((energy, eventMs, eventIndex) => {
      const distance = Math.abs(timeMs - eventMs);
      const width = kind === "game" ? 42 : 86 + (eventIndex % 3) * 12;
      const transient = Math.exp(-distance / width);
      return Math.max(energy, transient);
    }, 0);
    const texture =
      kind === "game"
        ? 0.025 + 0.018 * Math.abs(Math.sin(timeMs * 0.009))
        : 0.055 +
          0.035 * Math.abs(Math.sin(timeMs * 0.013)) +
          0.018 * Math.abs(Math.sin(timeMs * 0.031));
    const modulation = kind === "game" ? 1 : 0.78 + 0.22 * Math.abs(Math.sin(timeMs * 0.024));
    return Math.min(1, texture + eventEnergy * modulation * (kind === "game" ? 0.94 : 0.82));
  });
}
