import { z } from "zod";

export const judgmentDifficultySchema = z.enum(["Lenient", "Normal", "Strict"]);

export const judgmentCountsDtoSchema = z.object({
  Overload: z.number().int(),
  TooEarly: z.number().int(),
  Early: z.number().int(),
  EarlyPerfect: z.number().int(),
  Perfect: z.number().int(),
  LatePerfect: z.number().int(),
  Late: z.number().int(),
  TooLate: z.number().int(),
  Miss: z.number().int(),
});

export const levelSessionDtoSchema = z
  .object({
    Id: z.string(),
    LogicalLevelId: z.string(),
    LevelGroupId: z.string(),
    AppSessionId: z.string(),
    TufLevelId: z.number().int().nullable(),
    Song: z.string().nullable(),
    Author: z.string().nullable(),
    Artist: z.string().nullable(),
    OpenedAtUtc: z.string(),
    ClosedAtUtc: z.string().nullable(),
    FloorCount: z.number().int(),
    RunCount: z.number().int(),
    ClearRunCount: z.number().int(),
    NoFailRunCount: z.number().int(),
    FirstStartTile: z.number().int().nullable(),
    LastStartTile: z.number().int().nullable(),
    ChartAvailable: z.boolean(),
  })
  .passthrough();

export const logicalLevelDtoSchema = z
  .object({
    Id: z.string(),
    TufLevelId: z.number().int().nullable(),
    Song: z.string().nullable(),
    Author: z.string().nullable(),
    Artist: z.string().nullable(),
    FirstSeenAtUtc: z.string(),
    LastSeenAtUtc: z.string(),
    FloorCount: z.number().int(),
    VisitCount: z.number().int(),
    RunCount: z.number().int(),
    ClearRunCount: z.number().int(),
    NoFailRunCount: z.number().int(),
    FirstStartTile: z.number().int().nullable(),
    LastStartTile: z.number().int().nullable(),
    ChartAvailable: z.boolean(),
  })
  .passthrough();

export const appSessionDtoSchema = z
  .object({
    Id: z.string(),
    StartedAtUtc: z.string(),
    EndedAtUtc: z.string().nullable(),
    RecorderTimeZoneId: z.string().nullable(),
    RecorderUtcOffsetMinutes: z.number().int(),
    LevelSessions: z.array(levelSessionDtoSchema),
  })
  .passthrough();

export const activityRunDtoSchema = z
  .object({
    Id: z.string(),
    LevelSessionId: z.string(),
    TufLevelId: z.number().int().nullable(),
    RunIndex: z.number().int(),
    StartedAtUtc: z.string(),
    EndedAtUtc: z.string().nullable(),
    StartTile: z.number().int(),
    LastTile: z.number().int().nullable(),
    Result: z.string(),
    NoFailMode: z.boolean(),
    GameplayStartSongPosition: z.number().nullable(),
    LevelPitchPercent: z.number().nullable(),
    EffectivePitch: z.number().nullable(),
    XAccuracy: z.number().nullable(),
    JudgmentDifficulty: judgmentDifficultySchema.nullable(),
    JudgmentCounts: judgmentCountsDtoSchema,
    InputCount: z.number().int(),
    HitContextCount: z.number().int(),
    FloorCount: z.number().int(),
    InputBytes: z.number().int(),
    HitContextBytes: z.number().int(),
    HasMicrophoneRecording: z.boolean(),
    MicrophoneRecordingBytes: z.number().int(),
    MicrophoneDurationSeconds: z.number().nullable(),
    MicrophoneSampleRate: z.number().int().nullable(),
    MicrophoneChannels: z.number().int().nullable(),
    MicrophoneRecordingPermanent: z.boolean(),
    MicrophoneRecordingExpiresAtUtc: z.string().nullable(),
  })
  .passthrough();

export const activityChartDtoSchema = z
  .object({
    LevelSessionId: z.string(),
    LevelText: z.string(),
    FloorCount: z.number().int(),
  })
  .passthrough();

export const runDeleteResultDtoSchema = z.object({ RunId: z.string(), Deleted: z.boolean() });
export const recordingDeleteResultDtoSchema = runDeleteResultDtoSchema;
export const recordingKeepResultDtoSchema = z.object({ RunId: z.string(), Permanent: z.boolean() });

export type LevelSessionDto = z.infer<typeof levelSessionDtoSchema>;
export type LogicalLevelDto = z.infer<typeof logicalLevelDtoSchema>;
export type AppSessionDto = z.infer<typeof appSessionDtoSchema>;
export type ActivityRunDto = z.infer<typeof activityRunDtoSchema>;
export type ActivityChartDto = z.infer<typeof activityChartDtoSchema>;
