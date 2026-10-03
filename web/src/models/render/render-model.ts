import type { z } from "zod";
import type {
  outputDirectoryChoiceSchema,
  renderExportStatusSchema,
  renderHealthSchema,
  renderJobSchema,
  renderOptionsSchema,
  renderSettingsSchema,
} from "@/schemas/render/render-schema";

export type RenderHealth = z.infer<typeof renderHealthSchema>;
export type RenderExportStatus = z.infer<typeof renderExportStatusSchema>;
export type RenderJob = z.infer<typeof renderJobSchema>;
export type RenderOptions = z.infer<typeof renderOptionsSchema>;
export type RenderSettings = z.infer<typeof renderSettingsSchema>;
export type OutputDirectorySelection = z.infer<typeof outputDirectoryChoiceSchema>;
export const defaultRenderOptions: RenderOptions = {
  width: 1920,
  height: 1080,
  videoFps: 60,
  simulationFps: 240,
  bitrateMbps: 18,
  videoCodec: "H264",
  encoder: "Software",
  encoding: "Balanced",
  bitDepth: 8,
  proResProfile: "Standard",
  crf: null,
  pixelFormat: "auto",
  captureAudio: true,
  audioGainDb: 0,
  endDelaySeconds: 2,
  showRenderPreview: true,
  bgaMode: false,
  showPlanetRings: true,
  showSongTitle: true,
  showCountdown: true,
  showResultText: true,
  showHitJudgments: true,
  outputDirectory: "",
  includeWebcam: true,
  includeMicrophone: true,
  includeDmNote: true,
};

export function availableEncoders(
  codec: RenderOptions["videoCodec"],
  encoders: RenderOptions["encoder"][],
  engineOptions?: RenderSettings["engineOptions"],
): RenderOptions["encoder"][] {
  const definition = engineOptions?.codecs.find((value) => value.value === codec);
  if (definition) return definition.encoders;
  if (codec === "VP9") return encoders.filter((value) => value === "Software");
  if (codec === "ProRes")
    return encoders.filter(
      (value) => value === "Software" || value === "Auto" || value === "AppleVideoToolbox",
    );
  return encoders.filter((value) => value !== "AppleVideoToolbox");
}

export function availablePixelFormats(options: RenderOptions) {
  if (options.videoCodec !== "ProRes")
    return ["auto", options.bitDepth === 10 ? "yuv420p10le" : "yuv420p"] as const;
  const alpha =
    options.proResProfile === "FourFourFourFour" || options.proResProfile === "FourFourFourFourXQ";
  if (options.encoder === "AppleVideoToolbox") return ["auto", alpha ? "bgra" : "p210le"] as const;
  if (options.encoder === "Auto") return ["auto"] as const;
  return ["auto", alpha ? "yuva444p10le" : "yuv422p10le"] as const;
}
export function renderJobFinished(job: Pick<RenderJob, "state">) {
  return job.state === "completed" || job.state === "failed" || job.state === "cancelled";
}
