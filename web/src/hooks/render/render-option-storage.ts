import type { RenderOptions } from "@/models/render/render-model";
import { renderOptionsSchema } from "@/schemas/render/render-schema";

const storageKey = "tufreplay.render-options.v1";
const fields = [
  "width",
  "height",
  "videoFps",
  "simulationFps",
  "bitrateKbps",
  "videoCodec",
  "bitDepth",
  "captureAudio",
  "audioGainDb",
  "endDelaySeconds",
  "outputDirectory",
  "includeWebcam",
  "includeMicrophone",
] as const satisfies readonly (keyof RenderOptions)[];

export function readLastRenderOptions(defaults: RenderOptions): RenderOptions {
  try {
    const saved: unknown = JSON.parse(localStorage.getItem(storageKey) ?? "null");
    if (!saved || typeof saved !== "object" || Array.isArray(saved)) return defaults;
    const values = saved as Record<string, unknown>;
    const options = { ...defaults };
    for (const field of fields) {
      if (field in values) Object.assign(options, { [field]: values[field] });
    }
    // Saved before the bitrate moved to Kbps.
    if (!("bitrateKbps" in values) && typeof values.bitrateMbps === "number")
      options.bitrateKbps = values.bitrateMbps * 1_000;
    const result = renderOptionsSchema.safeParse(options);
    return result.success ? result.data : defaults;
  } catch {
    return defaults;
  }
}

/** How the file size row shows the bitrate: as megabytes each minute, or in Kbps. */
export type BitrateView = "fileSize" | "bitrate";

const bitrateViewKey = "tufreplay.render-bitrate-view.v1";

export function readBitrateView(): BitrateView {
  try {
    return localStorage.getItem(bitrateViewKey) === "bitrate" ? "bitrate" : "fileSize";
  } catch {
    return "fileSize";
  }
}

export function saveBitrateView(view: BitrateView): void {
  try {
    localStorage.setItem(bitrateViewKey, view);
  } catch {
    // The row still switches; it just starts as file size next time.
  }
}

export function saveLastRenderOptions(options: RenderOptions): void {
  try {
    const saved = Object.fromEntries(fields.map((field) => [field, options[field]]));
    localStorage.setItem(storageKey, JSON.stringify(saved));
  } catch {
    // Rendering remains available when browser storage is disabled.
  }
}
