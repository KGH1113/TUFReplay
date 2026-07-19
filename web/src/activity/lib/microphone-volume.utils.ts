export const MIN_MICROPHONE_VOLUME_DB = -20;
export const MAX_MICROPHONE_VOLUME_DB = 20;
export const DEFAULT_MICROPHONE_VOLUME_DB = 0;

export function clampMicrophoneVolumeDb(volumeDb: number) {
  if (!Number.isFinite(volumeDb)) return DEFAULT_MICROPHONE_VOLUME_DB;
  return Math.min(
    MAX_MICROPHONE_VOLUME_DB,
    Math.max(MIN_MICROPHONE_VOLUME_DB, Math.round(volumeDb)),
  );
}

export function microphoneDbToGain(volumeDb: number) {
  return 10 ** (clampMicrophoneVolumeDb(volumeDb) / 20);
}

export function formatMicrophoneVolumeDb(volumeDb: number) {
  const clampedVolumeDb = clampMicrophoneVolumeDb(volumeDb);
  return `${clampedVolumeDb > 0 ? "+" : ""}${clampedVolumeDb} dB`;
}
