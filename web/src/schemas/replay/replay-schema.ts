import { z } from "zod";

export const replayStateSchema = z.enum([
  "idle",
  "preparing",
  "opening_level",
  "waiting_for_focus",
  "starting",
  "playing",
  "returning_to_editor",
  "completed",
  "cancelled",
  "error",
]);

export const replayStatusDtoSchema = z
  .object({
    OperationId: z.string().nullable(),
    RunId: z.string().nullable(),
    State: replayStateSchema,
    ErrorCode: z.string().nullable(),
    Message: z.string().nullable(),
  })
  .passthrough();

export const replayLevelFilePickerOutcomeSchema = z.enum([
  "picking",
  "selected",
  "mismatch",
  "cancelled",
  "error",
]);

export const replayLevelFilePickerResultDtoSchema = z
  .object({
    OperationId: z.string().nullable(),
    RunId: z.string(),
    Outcome: replayLevelFilePickerOutcomeSchema,
    LevelPath: z.string().nullable(),
    ErrorCode: z.string().nullable(),
    Message: z.string().nullable(),
  })
  .passthrough();

export type ReplayStatusDto = z.infer<typeof replayStatusDtoSchema>;
export type ReplayLevelFilePickerResultDto = z.infer<typeof replayLevelFilePickerResultDtoSchema>;
