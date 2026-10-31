import type { RenderOptions } from "@/models/render/render-model";
import { renderOptionsSchema } from "@/schemas/render/render-schema";

const storageKey = "tufreplay.render-options.v1";
const fields = [
  "width",
  "height",
  "videoFps",
  "simulationFps",
  "bitrateMbps",
  "videoCodec",
  "bitDepth",
  "captureAudio",
  "audioGainDb",
  "endDelaySeconds",
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
    const result = renderOptionsSchema.safeParse(options);
    return result.success ? result.data : defaults;
  } catch {
    return defaults;
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
