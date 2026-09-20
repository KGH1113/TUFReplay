import { z } from "zod";
import { TUFREPLAY_BUILD_FLAVORS } from "@/shared/config/tufreplay-build-info";

export const healthDtoSchema = z
  .object({
    Ok: z.boolean(),
    Mod: z.string(),
    ModVersion: z.string(),
    ProtocolVersion: z.number().int(),
    ServerVersion: z.number().int(),
    ReplayEngineId: z.string(),
    ReplayFormatVersion: z.number().int(),
    BuildFlavor: z.enum(TUFREPLAY_BUILD_FLAVORS).optional().default("standard").catch("standard"),
    AutoSubmissionProtocolVersion: z.number().int().nonnegative().optional().default(0).catch(0),
  })
  .passthrough();

export type HealthDto = z.infer<typeof healthDtoSchema>;
