import { renderQualitySchema } from "@/schemas/render/render-schema";
import {
  availableEncoders,
  type RenderOptions,
  type RenderPreferences,
  type RenderSettings,
} from "./render-model";

export const renderQualityLevels = renderQualitySchema.options;
export type RenderQuality = (typeof renderQualityLevels)[number];
export type RenderSettingsMode = RenderPreferences["mode"];
type VideoSettings = Pick<
  RenderOptions,
  | "width"
  | "height"
  | "videoFps"
  | "simulationFps"
  | "bitrateMbps"
  | "videoCodec"
  | "encoder"
  | "encoding"
  | "bitDepth"
  | "proResProfile"
  | "crf"
  | "pixelFormat"
>;
export interface RenderRecommendation {
  quality: RenderQuality;
  video: VideoSettings;
  supported: boolean;
  megabytesPerMinute: number;
}

const profiles = [
  { width: 1280, height: 720, videoFps: 30, simulationFps: 120, bitrateMbps: 6 },
  { width: 1280, height: 720, videoFps: 60, simulationFps: 240, bitrateMbps: 10 },
  { width: 1920, height: 1080, videoFps: 60, simulationFps: 240, bitrateMbps: 18 },
  { width: 2560, height: 1440, videoFps: 60, simulationFps: 240, bitrateMbps: 32 },
  { width: 3840, height: 2160, videoFps: 60, simulationFps: 480, bitrateMbps: 60 },
  { width: 3840, height: 2160, videoFps: 120, simulationFps: 960, bitrateMbps: 100 },
] as const;

// These are conservative starting points, not a frame-time benchmark. Read the
// game host's capabilities; the browser may be running on another computer.
export function createRenderRecommendations(settings: RenderSettings | null) {
  const system = settings?.system;
  const offered = availableEncoders(
    "H264",
    settings?.capabilities.encoders ?? ["Software"],
    settings?.engineOptions,
  );
  const verified = system?.encoding.state === "ready" ? system.encoding.h264Encoders : [];
  const encoder =
    verified.find((value) => value !== "Software" && value !== "Auto" && offered.includes(value)) ??
    "Software";
  const hardware = encoder !== "Software";
  const threads = system?.logicalProcessors ?? 4;
  const memory = system?.memoryMb ?? 0;
  const videoMemory = system?.graphicsMemoryMb ?? 0;
  const unified = system?.platform === "macos" && /Apple M\d/i.test(system.processorName);
  let recommendedIndex = 2;
  if (system && ((memory > 0 && memory < 4096) || threads < 4)) recommendedIndex = 0;
  else if (system && ((memory > 0 && memory < 8192) || threads < 8)) recommendedIndex = 1;
  else if (hardware && memory >= 32768 && threads >= 16 && (videoMemory >= 8192 || unified))
    recommendedIndex = 4;
  else if (hardware && memory >= 16384 && threads >= 8 && (videoMemory >= 4096 || unified))
    recommendedIndex = 3;
  const levels: RenderRecommendation[] = renderQualityLevels.map((quality, index) => {
    const profile = profiles[index];
    const supported =
      (!settings || settings.capabilities.codecs.includes("H264")) &&
      (!system?.maxTextureSize || Math.max(profile.width, profile.height) <= system.maxTextureSize);
    return {
      quality,
      supported,
      // Target bitrate plus a small allowance for audio, before container overhead.
      megabytesPerMinute: Math.ceil(((profile.bitrateMbps + 0.4) * 60) / 8),
      video: {
        ...profile,
        videoCodec: "H264",
        encoder,
        encoding: hardware || threads >= 8 ? "Quality" : "Balanced",
        bitDepth: 8,
        proResProfile: "Standard",
        crf: null,
        pixelFormat: "auto",
      },
    };
  });
  while (recommendedIndex > 0 && !levels[recommendedIndex].supported) recommendedIndex--;
  return { levels, recommended: renderQualityLevels[recommendedIndex], encoder, hardware };
}

// Applying a quality preset changes video encoding only. Folder, recorded media,
// death delay, sound levels and game display choices stay owned by the user.
export function applyRenderRecommendation(
  options: RenderOptions,
  recommendations: ReturnType<typeof createRenderRecommendations>,
  quality: RenderQuality | null,
): RenderOptions {
  const selected = recommendations.levels.find(
    (value) => value.quality === (quality ?? recommendations.recommended),
  );
  return { ...options, ...selected?.video };
}
