import { z } from "zod";

export const videoCodecSchema = z.enum(["H264", "H265", "VP9", "AV1", "ProRes"]);
export const videoEncoderSchema = z.enum([
  "Auto",
  "Software",
  "NvidiaNvenc",
  "IntelQsv",
  "AmdAmf",
  "AppleVideoToolbox",
]);
export const encodingSpeedSchema = z.enum(["Maximum", "Balanced", "Quality"]);
export const proResProfileSchema = z.enum([
  "Proxy",
  "LT",
  "Standard",
  "HQ",
  "FourFourFourFour",
  "FourFourFourFourXQ",
]);
export const renderErrorDetailsSchema = z
  .object({
    field: z.string().nullable().optional(),
    line: z.number().int().positive().nullable().optional(),
    file: z.string().nullable().optional(),
  })
  .nullable()
  .optional();

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
  errorDetails: renderErrorDetailsSchema,
});
export const renderJobSchema = z.object({
  warnings: z.array(z.string()).optional().default([]),
  waitingForGameFocus: z.boolean().optional().default(false),
  waitingForFfmpeg: z.boolean().optional().default(false),
  jobId: z.string(),
  state: z.enum(["preparing", "rendering", "compositing", "completed", "failed", "cancelled"]),
  progress: z.number().min(0).max(1),
  errorMessage: z.string().nullable(),
  errorCode: z.string().nullable().optional().default(null),
  errorDetails: renderErrorDetailsSchema,
  outputFile: z.string().nullable(),
  outputDirectory: z.string().nullable().optional().default(null),
  localOutputPath: z.string().nullable().optional().default(null),
  canOpenOutput: z.boolean().optional().default(false),
  canDownload: z.boolean().optional().default(false),
});
export const renderOptionsSchema = z
  .object({
    width: z.number().int().min(320).max(7680).multipleOf(2),
    height: z.number().int().min(180).max(4320).multipleOf(2),
    videoFps: z.number().int().min(15).max(240),
    simulationFps: z.number().int().min(15).max(1024),
    bitrateMbps: z.number().int().min(1).max(200),
    videoCodec: videoCodecSchema,
    encoder: videoEncoderSchema,
    encoding: encodingSpeedSchema,
    bitDepth: z.union([z.literal(8), z.literal(10)]),
    proResProfile: proResProfileSchema,
    crf: z.number().int().min(0).max(63).nullable(),
    pixelFormat: z.enum([
      "auto",
      "yuv420p",
      "yuv420p10le",
      "yuv422p10le",
      "yuva444p10le",
      "p210le",
      "bgra",
    ]),
    captureAudio: z.boolean(),
    audioGainDb: z.number().min(-60).max(12),
    endDelaySeconds: z.number().min(0).max(30),
    showRenderPreview: z.boolean(),
    bgaMode: z.boolean(),
    showPlanetRings: z.boolean(),
    showSongTitle: z.boolean(),
    showCountdown: z.boolean(),
    showResultText: z.boolean(),
    showHitJudgments: z.boolean(),
    outputDirectory: z.string().max(4096),
    includeWebcam: z.boolean(),
    includeMicrophone: z.boolean(),
    includeDmNote: z.boolean(),
  })
  .strict()
  .superRefine((value, context) => {
    if (value.simulationFps < value.videoFps)
      context.addIssue({
        code: "custom",
        path: ["simulationFps"],
        message: "simulation_fps_below_video",
      });
    if (value.crf !== null) {
      if (value.encoder !== "Software" || value.videoCodec === "ProRes")
        context.addIssue({ code: "custom", path: ["crf"], message: "crf_unavailable" });
      else if ((value.videoCodec === "H264" || value.videoCodec === "H265") && value.crf > 51)
        context.addIssue({ code: "custom", path: ["crf"], message: "crf_out_of_range" });
    }
    if (value.videoCodec === "VP9" && value.encoder !== "Software")
      context.addIssue({ code: "custom", path: ["encoder"], message: "encoder_incompatible" });
    if (
      value.videoCodec === "ProRes" &&
      !["Software", "Auto", "AppleVideoToolbox"].includes(value.encoder)
    )
      context.addIssue({ code: "custom", path: ["encoder"], message: "encoder_incompatible" });
    if (value.videoCodec !== "ProRes" && value.encoder === "AppleVideoToolbox")
      context.addIssue({ code: "custom", path: ["encoder"], message: "encoder_incompatible" });
    if (value.pixelFormat !== "auto") {
      const alpha =
        value.proResProfile === "FourFourFourFour" || value.proResProfile === "FourFourFourFourXQ";
      const expected =
        value.videoCodec !== "ProRes"
          ? value.bitDepth === 10
            ? "yuv420p10le"
            : "yuv420p"
          : value.encoder === "AppleVideoToolbox"
            ? alpha
              ? "bgra"
              : "p210le"
            : alpha
              ? "yuva444p10le"
              : "yuv422p10le";
      if (value.pixelFormat !== expected)
        context.addIssue({
          code: "custom",
          path: ["pixelFormat"],
          message: "pixel_format_incompatible",
        });
    }
  });

export const renderSettingsSchema = z.object({
  defaults: renderOptionsSchema,
  outputDirectory: z.string(),
  capabilities: z.object({
    codecs: z.array(videoCodecSchema),
    encoders: z.array(videoEncoderSchema),
    bitDepths: z.array(z.union([z.literal(8), z.literal(10)])),
    proResProfiles: z.array(proResProfileSchema),
    pixelFormats: z.array(z.string()),
  }),
  engineOptions: z
    .object({
      codecs: z.array(z.object({ value: videoCodecSchema, encoders: z.array(videoEncoderSchema) })),
    })
    .optional(),
});
export const outputDirectoryChoiceSchema = z.object({
  selectionId: z.string(),
  pending: z.boolean(),
  outputDirectory: z.string().nullable(),
  errorCode: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
});
export const outputDirectoryCancelSchema = z.object({ cancelled: z.boolean() });
export const outputDirectoryOpenSchema = z.object({ opened: z.boolean() });
export const renderDownloadSchema = z.union([
  z.object({ Url: z.string() }).transform((value) => value.Url),
  z.object({ url: z.string() }).transform((value) => value.url),
]);
