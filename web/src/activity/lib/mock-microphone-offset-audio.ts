import type { MockMicrophoneOffsetCalibrationData } from "../mock/microphone-offset.mock";
import {
  clampMicrophoneVolumeDb,
  DEFAULT_MICROPHONE_VOLUME_DB,
  microphoneDbToGain,
} from "./microphone-volume.utils";

type AudioContextConstructor = new (contextOptions?: AudioContextOptions) => AudioContext;

const GAME_PREVIEW_GAIN = 0.18;
const MICROPHONE_PREVIEW_GAIN = 0.28;

export class MockMicrophoneOffsetAudioPlayer {
  private context: AudioContext | null = null;
  private gameBuffer: AudioBuffer | null = null;
  private microphoneBuffer: AudioBuffer | null = null;
  private gameGain: GainNode | null = null;
  private microphoneGain: GainNode | null = null;
  private gameSource: AudioBufferSourceNode | null = null;
  private microphoneSource: AudioBufferSourceNode | null = null;
  private timelineStartedAt = 0;
  private microphoneVolumeDb = DEFAULT_MICROPHONE_VOLUME_DB;

  constructor(private readonly data: MockMicrophoneOffsetCalibrationData) {}

  async play(offsetMs: number, microphoneVolumeDb: number) {
    this.updateMicrophoneVolume(microphoneVolumeDb);
    const context = await this.ensureContext();
    this.stopSources();

    const startAt = context.currentTime + 0.035;
    this.timelineStartedAt = startAt;
    this.gameSource = this.createSource(this.gameBuffer, this.gameGain);
    this.gameSource.start(startAt);
    this.scheduleMicrophone(offsetMs, 0, startAt);
  }

  updateOffset(offsetMs: number) {
    const context = this.context;
    if (!context || !this.microphoneBuffer || !this.microphoneGain) return;

    stopSource(this.microphoneSource);
    this.microphoneSource = null;
    const timelinePositionSeconds = Math.max(0, context.currentTime - this.timelineStartedAt);
    if (timelinePositionSeconds >= this.data.durationMs / 1_000) return;
    this.scheduleMicrophone(offsetMs, timelinePositionSeconds, context.currentTime);
  }

  updateMicrophoneVolume(volumeDb: number) {
    this.microphoneVolumeDb = clampMicrophoneVolumeDb(volumeDb);
    const context = this.context;
    const microphoneGain = this.microphoneGain;
    if (!context || !microphoneGain) return;
    const gain = MICROPHONE_PREVIEW_GAIN * microphoneDbToGain(this.microphoneVolumeDb);
    microphoneGain.gain.setTargetAtTime(gain, context.currentTime, 0.012);
  }

  stop() {
    this.stopSources();
  }

  dispose() {
    this.stopSources();
    this.gameGain?.disconnect();
    this.microphoneGain?.disconnect();
    this.gameGain = null;
    this.microphoneGain = null;
    const context = this.context;
    this.context = null;
    this.gameBuffer = null;
    this.microphoneBuffer = null;
    if (context && context.state !== "closed") void context.close();
  }

  private async ensureContext() {
    if (this.context) {
      if (this.context.state === "suspended") await this.context.resume();
      return this.context;
    }

    const AudioContextClass = getAudioContextConstructor();
    if (!AudioContextClass) throw new Error("Web Audio is not available in this browser.");

    const context = new AudioContextClass({ latencyHint: "interactive" });
    if (context.state === "suspended") await context.resume();
    this.context = context;
    this.gameGain = context.createGain();
    this.microphoneGain = context.createGain();
    this.gameGain.gain.value = GAME_PREVIEW_GAIN;
    this.microphoneGain.gain.value =
      MICROPHONE_PREVIEW_GAIN * microphoneDbToGain(this.microphoneVolumeDb);
    this.gameGain.connect(context.destination);
    this.microphoneGain.connect(context.destination);
    this.gameBuffer = createMockAudioBuffer(
      context,
      this.data.durationMs,
      this.data.gameEventsMs,
      "game",
    );
    this.microphoneBuffer = createMockAudioBuffer(
      context,
      this.data.durationMs,
      this.data.microphoneEventsMs,
      "microphone",
    );
    return context;
  }

  private createSource(buffer: AudioBuffer | null, gain: GainNode | null) {
    if (!this.context || !buffer || !gain) throw new Error("Mock audio is not ready.");
    const source = this.context.createBufferSource();
    source.buffer = buffer;
    source.connect(gain);
    return source;
  }

  private scheduleMicrophone(
    offsetMs: number,
    timelinePositionSeconds: number,
    destinationTime: number,
  ) {
    if (!this.microphoneBuffer || !this.microphoneGain) return;
    const offsetSeconds = offsetMs / 1_000;
    let sourcePositionSeconds = timelinePositionSeconds - offsetSeconds;
    let startAt = destinationTime;
    if (sourcePositionSeconds < 0) {
      startAt += -sourcePositionSeconds;
      sourcePositionSeconds = 0;
    }
    if (sourcePositionSeconds >= this.microphoneBuffer.duration) return;

    this.microphoneSource = this.createSource(this.microphoneBuffer, this.microphoneGain);
    this.microphoneSource.start(startAt, sourcePositionSeconds);
  }

  private stopSources() {
    stopSource(this.gameSource);
    stopSource(this.microphoneSource);
    this.gameSource = null;
    this.microphoneSource = null;
  }
}

function getAudioContextConstructor(): AudioContextConstructor | null {
  if (typeof window === "undefined") return null;
  return (
    window.AudioContext ??
    (window as unknown as { webkitAudioContext?: AudioContextConstructor }).webkitAudioContext ??
    null
  );
}

function createMockAudioBuffer(
  context: AudioContext,
  durationMs: number,
  eventsMs: number[],
  kind: "game" | "microphone",
) {
  const frameCount = Math.ceil((durationMs / 1_000) * context.sampleRate);
  const buffer = context.createBuffer(1, frameCount, context.sampleRate);
  const channel = buffer.getChannelData(0);
  const transientSeconds = kind === "game" ? 0.075 : 0.24;

  for (const [eventIndex, eventMs] of eventsMs.entries()) {
    const startFrame = Math.max(0, Math.round((eventMs / 1_000) * context.sampleRate));
    const transientFrames = Math.round(transientSeconds * context.sampleRate);
    for (let relativeFrame = 0; relativeFrame < transientFrames; relativeFrame += 1) {
      const frame = startFrame + relativeFrame;
      if (frame >= channel.length) break;
      const time = relativeFrame / context.sampleRate;
      const envelope = Math.exp(-time * (kind === "game" ? 47 : 14));
      const signal =
        kind === "game"
          ? Math.sin(time * Math.PI * 2 * 1_180) + 0.36 * Math.sin(time * Math.PI * 2 * 2_360)
          : 0.72 * Math.sin(time * Math.PI * 2 * (190 + eventIndex * 7)) +
            0.28 * deterministicNoise(relativeFrame, eventIndex);
      channel[frame] = Math.max(-1, Math.min(1, channel[frame] + signal * envelope));
    }
  }

  return buffer;
}

function deterministicNoise(frame: number, eventIndex: number) {
  const value = Math.sin((frame + 1) * (eventIndex + 3) * 12.9898) * 43_758.5453;
  return (value - Math.floor(value)) * 2 - 1;
}

function stopSource(source: AudioBufferSourceNode | null) {
  if (!source) return;
  try {
    source.stop();
  } catch {
    // A source that has already ended does not need additional cleanup.
  }
  source.disconnect();
}
