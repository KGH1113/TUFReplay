import { z } from "zod";

export const renderHealthSchema = z.object({
  available: z.boolean(),
  version: z.string(),
  schemaVersion: z.literal(1),
  busy: z.boolean(),
  dmNoteConfigured: z.boolean(),
  orbitAvailable: z.boolean(),
});
export const renderExportStatusSchema = z.object({
  jobId: z.string(),
  runId: z.string(),
  state: z.enum(["preparing", "completed", "failed", "cancelled"]),
  progress: z.number().min(0).max(1),
  manifestPath: z.string().nullable(),
  errorCode: z.string().nullable(),
  errorMessage: z.string().nullable(),
});
export const renderJobSchema = z.object({
  warnings: z.array(z.string()).optional().default([]),
  jobId: z.string(),
  state: z.enum(["preparing", "rendering", "compositing", "completed", "failed", "cancelled"]),
  progress: z.number().min(0).max(1),
  errorMessage: z.string().nullable(),
  outputFile: z.string().nullable(),
  canDownload: z.boolean(),
});
export const renderOptionsSchema = z
  .object({
    width: z.number().int().min(320).max(7680).multipleOf(2),
    height: z.number().int().min(180).max(4320).multipleOf(2),
    fps: z.union([z.literal(30), z.literal(60), z.literal(120)]),
    includeWebcam: z.boolean(),
    includeMicrophone: z.boolean(),
    includeDmNote: z.boolean(),
  })
  .strict();
export const renderDownloadSchema = z.union([
  z.object({ Url: z.string() }).transform((value) => value.Url),
  z.object({ url: z.string() }).transform((value) => value.url),
]);
