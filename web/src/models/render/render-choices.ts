import { availableEncoders, type RenderOptions } from "@/models/render/render-model";

/** Video sizes offered as plain choices; any other size is a custom size. */
export const qualityChoices = [
  { id: "720", width: 1280, height: 720, label: "720p" },
  { id: "1080", width: 1920, height: 1080, label: "1080p" },
  { id: "1440", width: 2560, height: 1440, label: "1440p" },
  { id: "2160", width: 3840, height: 2160, label: "4K" },
] as const;

export type QualityChoice = (typeof qualityChoices)[number]["id"];

/** The plain choice matching the video size, or null for a custom size. */
export function qualityOf(options: Pick<RenderOptions, "width" | "height">): QualityChoice | null {
  return (
    qualityChoices.find(
      (choice) => choice.width === options.width && choice.height === options.height,
    )?.id ?? null
  );
}

export function withQuality(options: RenderOptions, quality: QualityChoice): RenderOptions {
  const choice = qualityChoices.find((item) => item.id === quality);
  return choice ? { ...options, width: choice.width, height: choice.height } : options;
}

/** Video frames per second offered as plain choices; 240 is OrbitRender's limit. */
export const smoothnessChoices = [60, 120, 240] as const;

/** Game steps per second. Never below the video's frames per second. */
export const precisionChoices = [60, 120, 240, 480, 960] as const;

/** The largest video OrbitRender makes for TUFReplay (the mod checks the same limits). */
export const maxVideoSize = { width: 3840, height: 2160 } as const;
export const minVideoSize = { width: 320, height: 180 } as const;

/** The game must step at least as often as the video takes frames, so it keeps up. */
export function withVideoFps(options: RenderOptions, videoFps: number): RenderOptions {
  return { ...options, videoFps, simulationFps: Math.max(options.simulationFps, videoFps) };
}

export const formatChoices = ["H264", "H265", "AV1", "VP9", "ProRes"] as const;

/** ProRes profiles from lightest to heaviest. */
export const proResChoices = [
  "Proxy",
  "LT",
  "Standard",
  "HQ",
  "FourFourFourFour",
  "FourFourFourFourXQ",
] as const satisfies readonly RenderOptions["proResProfile"][];

/** Profile names as people and OrbitRender write them. */
export const proResNames: Record<RenderOptions["proResProfile"], string> = {
  Proxy: "Proxy",
  LT: "LT",
  Standard: "Standard",
  HQ: "HQ",
  FourFourFourFour: "4444",
  FourFourFourFourXQ: "4444 XQ",
};

/** Format names as players know them; they are the same in every language. */
export const formatNames: Record<RenderOptions["videoCodec"], string> = {
  H264: "H.264",
  H265: "H.265",
  VP9: "VP9",
  AV1: "AV1",
  ProRes: "ProRes",
};

/** The bitrates OrbitRender takes, in kilobits each second. */
export const bitrateRangeKbps = { min: 1_000, max: 200_000 } as const;

/** A bitrate (kilobits each second) as the file it makes: megabytes each minute. */
export function megabytesPerMinute(bitrateKbps: number) {
  return Math.round((bitrateKbps * 60) / 8 / 1_000);
}

/** The bitrate (kilobits each second) that makes this many megabytes each minute. */
export function bitrateForMegabytesPerMinute(megabytes: number) {
  return (megabytes * 1_000 * 8) / 60;
}

/** A number as people type it, with or without digit grouping; null when it is not one. */
export function readTypedNumber(text: string): number | null {
  const plain = text.replace(/[\s,]/g, "");
  if (!/^\d+(\.\d*)?$|^\.\d+$/.test(plain)) return null;
  return Number(plain);
}

/**
 * A bitrate kept in range and on the steps the renderer takes: every Kbps, or
 * whole Mbps for an OrbitRender that rounds to them.
 */
export function settleBitrate(bitrateKbps: number, stepKbps: number) {
  const stepped = Math.round(bitrateKbps / stepKbps) * stepKbps;
  return Math.min(bitrateRangeKbps.max, Math.max(bitrateRangeKbps.min, stepped));
}

/** Switches the video format and keeps the other encoding settings valid for it. */
export function withVideoCodec(
  options: RenderOptions,
  videoCodec: RenderOptions["videoCodec"],
): RenderOptions {
  const encoderFits = availableEncoders(videoCodec, [options.encoder]).includes(options.encoder);
  return {
    ...options,
    videoCodec,
    encoder: encoderFits ? options.encoder : "Software",
    // ProRes is always 10-bit.
    bitDepth: videoCodec === "ProRes" ? 10 : options.bitDepth,
    pixelFormat: "auto",
    crf: null,
  };
}

/** A folder as it is shown: its own name first, then the folder it sits in. */
export function describeFolder(path: string): { name: string; parent: string; path: string } {
  const trimmed = path.trim().replace(/[\\/]+$/, "");
  const name = trimmed.split(/[\\/]/).filter(Boolean).at(-1) ?? trimmed;
  const parent = trimmed.slice(0, trimmed.length - name.length).replace(/[\\/]+$/, "");
  return { name, parent, path: trimmed };
}
