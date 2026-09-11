import { z } from "zod";

export const submissionStatusSchema = z.object({
  connected: z.boolean(),
  configured: z.boolean().default(false),
  disabled: z.boolean().default(false),
  state: z.string(),
  runId: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
});
export const submissionRunSchema = z.object({
  cursor: z.number().int(),
  run_id: z.uuid(),
  tuf_level_id: z.number().int().positive(),
  chart_path: z.string(),
  status: z.string(),
  reason: z.string().nullable(),
  external_pass_id: z.number().int().positive().nullable(),
  created_at: z.string(),
  evidence_expires_at: z.string().nullable().optional(),
});
export const submissionPageSchema = z.object({
  runs: z.array(submissionRunSchema),
  next_cursor: z.number().int().nullable(),
});
