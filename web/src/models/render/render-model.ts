import type { z } from "zod";
import type {
  renderExportStatusSchema,
  renderHealthSchema,
  renderJobSchema,
  renderOptionsSchema,
} from "@/schemas/render/render-schema";

export type RenderHealth = z.infer<typeof renderHealthSchema>;
export type RenderExportStatus = z.infer<typeof renderExportStatusSchema>;
export type RenderJob = z.infer<typeof renderJobSchema>;
export type RenderOptions = z.infer<typeof renderOptionsSchema>;
export const defaultRenderOptions: RenderOptions = {
  width: 1920,
  height: 1080,
  fps: 60,
  includeWebcam: true,
  includeMicrophone: true,
  includeDmNote: true,
};
export function renderJobFinished(job: Pick<RenderJob, "state">) {
  return job.state === "completed" || job.state === "failed" || job.state === "cancelled";
}
