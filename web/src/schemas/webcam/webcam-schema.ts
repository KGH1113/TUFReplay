import { z } from "zod";

export const cameraCropSchema = z
  .object({
    x: z.number().min(0).max(1),
    y: z.number().min(0).max(1),
    width: z.number().min(0.05).max(1),
    height: z.number().min(0.05).max(1),
  })
  .strict()
  .refine((crop) => crop.x + crop.width <= 1.00000001 && crop.y + crop.height <= 1.00000001);

const cameraCropDtoSchema = z
  .object({ X: z.number(), Y: z.number(), Width: z.number(), Height: z.number() })
  .transform((crop) => ({ x: crop.X, y: crop.Y, width: crop.Width, height: crop.Height }))
  .pipe(cameraCropSchema);

export const webcamSettingsPatchSchema = z
  .object({
    enabled: z.boolean().optional(),
    deviceId: z.string().nullable().optional(),
    quality: z.enum(["compact", "balanced", "quality"]).optional(),
    offsetMs: z.number().int().min(-1000).max(1000).optional(),
    storageLimitMb: z.number().int().min(64).max(8192).optional(),
    retentionDays: z.number().int().min(1).max(30).optional(),
    playbackVisible: z.boolean().optional(),
    liveVisible: z.boolean().optional(),
    mirror: z.boolean().optional(),
    crop: cameraCropSchema.optional(),
  })
  .strict();

export const webcamStateDtoSchema = z.object({
  Supported: z.boolean(),
  Backend: z.enum(["avfoundation", "ffmpeg", "unsupported"]),
  Enabled: z.boolean(),
  CaptureLocked: z.boolean(),
  Status: z.enum(["off", "warming", "ready", "recording", "saving", "error"]),
  Error: z.string().nullable(),
  Devices: z.array(z.object({ Id: z.string(), Name: z.string() })),
  SelectedDeviceId: z.string().nullable(),
  Quality: z.enum(["compact", "balanced", "quality"]),
  OffsetMs: z.number().int().min(-1000).max(1000),
  StorageLimitMb: z.number().int().min(64).max(8192),
  RetentionDays: z.number().int().min(1).max(30),
  PlaybackVisible: z.boolean(),
  LiveVisible: z.boolean().default(false),
  Mirror: z.boolean(),
  Crop: cameraCropDtoSchema.default({ x: 0, y: 0, width: 1, height: 1 }),
});

export type WebcamStateDto = z.infer<typeof webcamStateDtoSchema>;
export type CameraCrop = z.infer<typeof cameraCropSchema>;
export type WebcamSettingsPatch = z.infer<typeof webcamSettingsPatchSchema>;
