// Mirrors OBS Studio's Gain filter: -30 dB to +30 dB in 0.1 dB steps, applied
// as a linear multiplier. The mod rounds saved values the same way.
export const MIN_MICROPHONE_VOLUME_DB = -30;
export const MAX_MICROPHONE_VOLUME_DB = 30;
export const MICROPHONE_VOLUME_STEP_DB = 0.1;
export const MICROPHONE_VOLUME_LARGE_STEP_DB = 1;
export const DEFAULT_MICROPHONE_VOLUME_DB = 0;

const STEPS_PER_DB = 10;

export function clampMicrophoneVolumeDb(volumeDb: number) {
  if (Number.isNaN(volumeDb)) return DEFAULT_MICROPHONE_VOLUME_DB;
  const clamped = Math.min(MAX_MICROPHONE_VOLUME_DB, Math.max(MIN_MICROPHONE_VOLUME_DB, volumeDb));
  // Round half away from zero, matching the mod's MidpointRounding.AwayFromZero.
  const rounded =
    (Math.sign(clamped) * Math.round(Math.abs(clamped) * STEPS_PER_DB)) / STEPS_PER_DB;
  return rounded === 0 ? 0 : rounded;
}

// OBS db_to_mul: powf(10, db / 20) in single precision.
export function microphoneDbToGain(volumeDb: number) {
  const volume = Math.fround(clampMicrophoneVolumeDb(volumeDb));
  return Math.fround(10 ** Math.fround(volume / 20));
}

export function formatMicrophoneVolumeDb(volumeDb: number) {
  const clampedVolumeDb = clampMicrophoneVolumeDb(volumeDb);
  return `${clampedVolumeDb > 0 ? "+" : ""}${clampedVolumeDb.toFixed(1)} dB`;
}
