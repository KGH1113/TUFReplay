export const MIN_MICROPHONE_OFFSET_MS = -500;
export const MAX_MICROPHONE_OFFSET_MS = 500;

export function clampMicrophoneOffset(offsetMs: number) {
  return Math.min(
    MAX_MICROPHONE_OFFSET_MS,
    Math.max(MIN_MICROPHONE_OFFSET_MS, Math.round(offsetMs)),
  );
}

export function offsetFromPointerDelta(
  startOffsetMs: number,
  deltaPixels: number,
  timelineWidthPixels: number,
  durationMs: number,
) {
  if (timelineWidthPixels <= 0 || durationMs <= 0) return clampMicrophoneOffset(startOffsetMs);
  return clampMicrophoneOffset(startOffsetMs + (deltaPixels / timelineWidthPixels) * durationMs);
}

export function keyboardOffsetAdjustment(offsetMs: number, key: string, largeStep: boolean) {
  const step = largeStep ? 10 : 1;
  if (key === "ArrowLeft") return clampMicrophoneOffset(offsetMs - step);
  if (key === "ArrowRight") return clampMicrophoneOffset(offsetMs + step);
  return null;
}

export function formatMicrophoneOffset(offsetMs: number) {
  if (offsetMs === 0) return "0 ms";
  return `${offsetMs > 0 ? "+" : "−"}${Math.abs(offsetMs)} ms`;
}

export function buildWaveformAreaPath(samples: number[], width = 1_000, height = 100) {
  if (!samples.length) return "";
  const center = height / 2;
  const upper = samples.map((sample, index) => {
    const x = samples.length === 1 ? 0 : (index / (samples.length - 1)) * width;
    const y = center - Math.max(0, Math.min(1, sample)) * center * 0.88;
    return `${index === 0 ? "M" : "L"}${roundPathNumber(x)},${roundPathNumber(y)}`;
  });
  const lower = [...samples].reverse().map((sample, reverseIndex) => {
    const sourceIndex = samples.length - reverseIndex - 1;
    const x = samples.length === 1 ? 0 : (sourceIndex / (samples.length - 1)) * width;
    const y = center + Math.max(0, Math.min(1, sample)) * center * 0.88;
    return `L${roundPathNumber(x)},${roundPathNumber(y)}`;
  });
  return `${upper.join(" ")} ${lower.join(" ")} Z`;
}

function roundPathNumber(value: number) {
  return Math.round(value * 100) / 100;
}
