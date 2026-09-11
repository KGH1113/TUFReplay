import { z } from "zod";

export const calibrationStateSchema = z.enum([
  "idle",
  "arming",
  "opening_level",
  "waiting_for_run",
  "recording",
  "processing",
  "editing",
  "preview_starting",
  "preview_playing",
  "error",
]);

export const calibrationStatusDtoSchema = z
  .object({
    OperationId: z.string().nullable(),
    State: calibrationStateSchema,
    ErrorCode: z.string().nullable(),
    Message: z.string().nullable(),
    DurationMs: z.number(),
    PlaybackPositionMs: z.number(),
    ResultRevision: z.number().int(),
    MicrophoneOffsetMs: z.number(),
    MicrophoneVolumeDb: z.number(),
  })
  .passthrough();

export const calibrationResultDtoSchema = z
  .object({
    OperationId: z.string(),
    Revision: z.number().int(),
    DurationMs: z.number(),
    GameWaveform: z.array(z.number()),
    SongWaveform: z.array(z.number()).optional(),
    MicrophoneWaveform: z.array(z.number()),
  })
  .passthrough();

export type CalibrationStatusDto = z.infer<typeof calibrationStatusDtoSchema>;
export type CalibrationResultDto = z.infer<typeof calibrationResultDtoSchema>;
