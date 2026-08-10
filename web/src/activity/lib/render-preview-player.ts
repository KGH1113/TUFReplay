/**
 * Plays the audio-only render preview's stems with live per-source gain.
 *
 * The mod returns raw music / hitsound / microphone WAVs (no volume baked in); this player runs
 * each through its own GainNode, so moving a volume slider is heard immediately — during playback
 * and on every replay — without re-rendering. The mix here is stems × gains, which matches the
 * render's mix before its mastering chain; close enough to dial in a balance.
 */
export interface RenderPreviewStems {
  MusicWavBase64: string;
  HitsoundsWavBase64: string;
  MicrophoneWavBase64: string | null;
}

export interface RenderPreviewLevels {
  music: number;
  hitsound: number;
  microphone: number;
}

/** Positive plays the microphone earlier — the calibration offset's convention. */
export type MicTimingMs = number;

export class RenderPreviewPlayer {
  private context: AudioContext | null = null;
  private buffers: {
    music: AudioBuffer;
    hitsounds: AudioBuffer;
    microphone: AudioBuffer | null;
  } | null = null;
  private gains: { music: GainNode; hitsound: GainNode; microphone: GainNode } | null = null;
  private sources: AudioBufferSourceNode[] = [];
  private microphoneSource: AudioBufferSourceNode | null = null;
  private playbackStartTime = 0;
  private micTimingSec = 0;
  private playing = false;
  private onEnded: (() => void) | null = null;

  get hasStems(): boolean {
    return this.buffers !== null;
  }

  get isPlaying(): boolean {
    return this.playing;
  }

  /** Decodes the stems into playable buffers. Replaces any previously loaded preview. */
  async load(stems: RenderPreviewStems): Promise<void> {
    this.stop();
    if (!this.context) this.context = new AudioContext();
    const context = this.context;

    const decode = (base64: string) => context.decodeAudioData(base64ToArrayBuffer(base64));
    const [music, hitsounds, microphone] = await Promise.all([
      decode(stems.MusicWavBase64),
      decode(stems.HitsoundsWavBase64),
      stems.MicrophoneWavBase64 ? decode(stems.MicrophoneWavBase64) : Promise.resolve(null),
    ]);
    this.buffers = { music, hitsounds, microphone };
  }

  /** Starts playback from the top at the given levels. */
  play(levels: RenderPreviewLevels, onEnded: () => void): boolean {
    if (!this.buffers || !this.context) return false;
    this.stop();

    const context = this.context;
    void context.resume();

    this.gains = {
      music: context.createGain(),
      hitsound: context.createGain(),
      microphone: context.createGain(),
    };
    this.setLevels(levels);
    for (const gain of Object.values(this.gains)) gain.connect(context.destination);

    const startSource = (buffer: AudioBuffer, gain: GainNode) => {
      const source = context.createBufferSource();
      source.buffer = buffer;
      source.connect(gain);
      source.start();
      this.sources.push(source);
      return source;
    };

    this.playbackStartTime = context.currentTime;
    const music = startSource(this.buffers.music, this.gains.music);
    startSource(this.buffers.hitsounds, this.gains.hitsound);
    if (this.buffers.microphone) this.startMicrophoneSource();

    this.onEnded = onEnded;
    music.onended = () => {
      if (this.onEnded && this.playing) {
        this.playing = false;
        this.onEnded();
      }
    };
    this.playing = true;
    return true;
  }

  /**
   * Live microphone timing trim. Restarts only the microphone source at the corrected position,
   * so dragging the slider mid-playback shifts the mic against the music immediately.
   */
  setMicTiming(timingMs: MicTimingMs): void {
    this.micTimingSec = timingMs / 1000;
    if (this.playing && this.buffers?.microphone) this.startMicrophoneSource();
  }

  private startMicrophoneSource(): void {
    const context = this.context;
    const buffer = this.buffers?.microphone;
    const gain = this.gains?.microphone;
    if (!context || !buffer || !gain) return;

    if (this.microphoneSource) {
      try {
        this.microphoneSource.stop();
      } catch {
        // Already ended.
      }
      this.microphoneSource.disconnect();
      this.sources = this.sources.filter((source) => source !== this.microphoneSource);
      this.microphoneSource = null;
    }

    // Positive timing = mic earlier: at context time T the mic buffer position is
    // (T - playbackStart) + timing. A negative position means it has not started yet.
    const source = context.createBufferSource();
    source.buffer = buffer;
    source.connect(gain);
    const position = context.currentTime - this.playbackStartTime + this.micTimingSec;
    if (position >= 0) source.start(0, position);
    else source.start(context.currentTime - position, 0);
    this.sources.push(source);
    this.microphoneSource = source;
  }

  /** Applies the sliders. Safe to call at any time; audible immediately while playing. */
  setLevels(levels: RenderPreviewLevels): void {
    if (!this.gains) return;
    this.gains.music.gain.value = Math.max(0, levels.music);
    this.gains.hitsound.gain.value = Math.max(0, levels.hitsound);
    this.gains.microphone.gain.value = Math.max(0, levels.microphone);
  }

  stop(): void {
    this.onEnded = null;
    this.playing = false;
    for (const source of this.sources) {
      try {
        source.stop();
      } catch {
        // Already ended.
      }
      source.disconnect();
    }
    this.sources = [];
    this.microphoneSource = null;
    if (this.gains) {
      for (const gain of Object.values(this.gains)) gain.disconnect();
      this.gains = null;
    }
  }

  /** Drops the loaded stems (dialog closed / new run). */
  clear(): void {
    this.stop();
    this.buffers = null;
  }
}

function base64ToArrayBuffer(base64: string): ArrayBuffer {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}
