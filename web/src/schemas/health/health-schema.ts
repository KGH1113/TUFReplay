import { z } from "zod";

export const healthDtoSchema = z
  .object({
    Ok: z.boolean(),
    Mod: z.string(),
    ModVersion: z.string(),
    ProtocolVersion: z.number().int(),
    ServerVersion: z.number().int(),
    ReplayEngineId: z.string(),
    ReplayFormatVersion: z.number().int(),
  })
  .passthrough();

export type HealthDto = z.infer<typeof healthDtoSchema>;
