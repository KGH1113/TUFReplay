import type { MicrophoneOffsetCalibrationData } from "@/models/calibration/microphone-offset-calibration-data";

export interface MockMicrophoneOffsetCalibrationData extends MicrophoneOffsetCalibrationData {
  durationMs: number;
  initialOffsetMs: number;
  suggestedOffsetMs: number;
  gameEventsMs: number[];
  microphoneEventsMs: number[];
  gameWaveform: number[];
  songWaveform: number[];
  microphoneWaveform: number[];
}

const DURATION_MS = 6_000;
const SUGGESTED_OFFSET_MS = 96;
const WAVEFORM_SAMPLE_COUNT = 2048;
const GAME_EVENTS_MS = [520, 1_180, 1_920, 2_760, 3_420, 4_260, 5_140];
const MICROPHONE_EVENTS_MS = GAME_EVENTS_MS.map((eventMs) => eventMs + SUGGESTED_OFFSET_MS);

const songWaveform = createInputWaveform(GAME_EVENTS_MS);

export const mockMicrophoneOffsetCalibration: MockMicrophoneOffsetCalibrationData = {
  durationMs: DURATION_MS,
  initialOffsetMs: 0,
  suggestedOffsetMs: SUGGESTED_OFFSET_MS,
  gameEventsMs: GAME_EVENTS_MS,
  microphoneEventsMs: MICROPHONE_EVENTS_MS,
  gameWaveform: songWaveform,
  songWaveform,
  microphoneWaveform: createMicrophoneWaveform(MICROPHONE_EVENTS_MS),
};

function createInputWaveform(eventsMs: number[]) {
  return Array.from({ length: WAVEFORM_SAMPLE_COUNT }, (_, index) => {
    const timeMs = (index / (WAVEFORM_SAMPLE_COUNT - 1)) * DURATION_MS;
    return eventsMs.reduce((energy, eventMs) => {
      const distance = Math.abs(timeMs - eventMs);
      return Math.max(energy, Math.exp(-distance / 12));
    }, 0);
  });
}

function createMicrophoneWaveform(eventsMs: number[]) {
  return Array.from({ length: WAVEFORM_SAMPLE_COUNT }, (_, index) => {
    const timeMs = (index / (WAVEFORM_SAMPLE_COUNT - 1)) * DURATION_MS;
    const eventEnergy = eventsMs.reduce((energy, eventMs, eventIndex) => {
      const distance = Math.abs(timeMs - eventMs);
      const width = 86 + (eventIndex % 3) * 12;
      const transient = Math.exp(-distance / width);
      return Math.max(energy, transient);
    }, 0);
    const texture =
      0.055 +
      0.035 * Math.abs(Math.sin(timeMs * 0.013)) +
      0.018 * Math.abs(Math.sin(timeMs * 0.031));
    const modulation = 0.78 + 0.22 * Math.abs(Math.sin(timeMs * 0.024));
    return Math.min(1, texture + eventEnergy * modulation * 0.82);
  });
}
