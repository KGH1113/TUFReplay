import { z } from "zod";

export const visualKindSchema = z.enum(["keyviewer", "overlay"]);
export const visualSourceSchema = z.enum([
  "jipper-resourcepack",
  "dmnote",
  "impl-dmnote",
  "jipper-keyviewer",
  "impl-resourcepack",
]);

export const visualPresetSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1),
  kind: visualKindSchema,
  source: visualSourceSchema,
  source_version: z.string().min(1),
  created_at: z.string().min(1),
});

export const visualPresetPageSchema = z.object({
  presets: z.array(visualPresetSchema),
});

export const visualSourceInfoSchema = z.object({
  source: visualSourceSchema,
  version: z.string().min(1),
  available: z.boolean(),
  kinds: z.array(visualKindSchema),
});

export const visualSourcesSchema = z.object({
  sources: z.array(visualSourceInfoSchema),
});

export const visualPresetResponseSchema = z.object({
  preset: visualPresetSchema,
});

export const visualRemoveResponseSchema = z.object({
  deleted: z.literal(true),
});

export const visualInspectionSchema = z.object({
  missing_assets: z.array(
    z.object({ reference: z.string().min(1), kind: z.enum(["font", "image", "asset"]) }),
  ),
  asset_count: z.number().int().nonnegative(),
});

const missingAssetsSchema = visualInspectionSchema.shape.missing_assets;
export const visualRegistrationProgressSchema = z.object({
  stage: z.enum(["preparing", "reading_settings", "processing_assets", "validating", "uploading"]),
  asset_name: z.string().nullable().optional(),
  completed_assets: z.number().int().nonnegative().default(0),
});
export const visualRegistrationStartSchema = z.object({ operation_id: z.string().uuid() });
export const visualRegistrationSchema = z.discriminatedUnion("state", [
  z.object({ state: z.literal("running"), progress: visualRegistrationProgressSchema }),
  z.object({
    state: z.literal("needs_assets"),
    progress: visualRegistrationProgressSchema,
    missing_assets: missingAssetsSchema,
  }),
  z.object({
    state: z.literal("completed"),
    progress: visualRegistrationProgressSchema,
    preset: visualPresetSchema,
  }),
  z.object({
    state: z.literal("failed"),
    progress: visualRegistrationProgressSchema,
    error: z.object({ code: z.string() }),
  }),
]);
