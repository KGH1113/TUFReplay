import type {
  ActivityChartDto,
  ActivityRunDto,
  AppSessionDto,
  LevelSessionDto,
  LogicalLevelDto,
} from "@/schemas/activity/activity-schema";

export type JudgmentDifficulty = NonNullable<ActivityRunDto["JudgmentDifficulty"]>;
export type JudgmentCounts = ActivityRunDto["JudgmentCounts"];

export type LevelSession = ReturnType<typeof mapLevelSession>;
export type LogicalLevel = ReturnType<typeof mapLogicalLevel>;
export type AppSession = ReturnType<typeof mapAppSession>;
export type ActivityRun = ReturnType<typeof mapActivityRun>;
export type ActivityChart = ReturnType<typeof mapActivityChart>;

export type ConnectionStatus = "connecting" | "online" | "incompatible" | "error";

export interface RunMarker {
  id: string;
  floorIndex: number;
  count: number;
  clearCount: number;
  bestLastFloorIndex: number;
}

export interface LevelMetadata {
  levelId: number | null;
  artist: string;
  name: string;
  creator: string;
  difficulty: string;
  difficultyIconUrl: string;
  source: "tuf" | "local" | "fallback";
}

export interface LevelCard extends LogicalLevel {
  levelGroupId: string;
  canOpen: boolean;
  visibleRunCount: number;
  hiddenRunCount: number;
}

export interface ActivityDay {
  date: string;
  appSessions: AppSession[];
  levelSessions: LevelCard[];
  hasOpenableLevels: boolean;
  runCount: number;
  clearRunCount: number;
}

export function mapLevelSession(dto: LevelSessionDto) {
  return {
    id: dto.Id,
    logicalLevelId: dto.LogicalLevelId,
    levelGroupId: dto.LevelGroupId,
    appSessionId: dto.AppSessionId,
    tufLevelId: dto.TufLevelId,
    song: dto.Song,
    author: dto.Author,
    artist: dto.Artist,
    openedAtUtc: dto.OpenedAtUtc,
    closedAtUtc: dto.ClosedAtUtc,
    floorCount: dto.FloorCount,
    runCount: dto.RunCount,
    clearRunCount: dto.ClearRunCount,
    noFailRunCount: dto.NoFailRunCount,
    firstStartTile: dto.FirstStartTile,
    lastStartTile: dto.LastStartTile,
    chartAvailable: dto.ChartAvailable,
  };
}

export function mapLogicalLevel(dto: LogicalLevelDto) {
  return {
    id: dto.Id,
    tufLevelId: dto.TufLevelId,
    song: dto.Song,
    author: dto.Author,
    artist: dto.Artist,
    firstSeenAtUtc: dto.FirstSeenAtUtc,
    lastSeenAtUtc: dto.LastSeenAtUtc,
    floorCount: dto.FloorCount,
    visitCount: dto.VisitCount,
    runCount: dto.RunCount,
    clearRunCount: dto.ClearRunCount,
    noFailRunCount: dto.NoFailRunCount,
    firstStartTile: dto.FirstStartTile,
    lastStartTile: dto.LastStartTile,
    chartAvailable: dto.ChartAvailable,
  };
}

export function mapAppSession(dto: AppSessionDto) {
  return {
    id: dto.Id,
    startedAtUtc: dto.StartedAtUtc,
    endedAtUtc: dto.EndedAtUtc,
    recorderTimeZoneId: dto.RecorderTimeZoneId,
    recorderUtcOffsetMinutes: dto.RecorderUtcOffsetMinutes,
    levelSessions: dto.LevelSessions.map(mapLevelSession),
  };
}

export function mapActivityRun(dto: ActivityRunDto) {
  return {
    id: dto.Id,
    levelSessionId: dto.LevelSessionId,
    tufLevelId: dto.TufLevelId,
    runIndex: dto.RunIndex,
    startedAtUtc: dto.StartedAtUtc,
    endedAtUtc: dto.EndedAtUtc,
    startTile: dto.StartTile,
    lastTile: dto.LastTile,
    result: dto.Result,
    noFailMode: dto.NoFailMode,
    gameplayStartSongPosition: dto.GameplayStartSongPosition,
    levelPitchPercent: dto.LevelPitchPercent,
    effectivePitch: dto.EffectivePitch,
    xAccuracy: dto.XAccuracy,
    judgmentDifficulty: dto.JudgmentDifficulty,
    judgmentSystem: dto.JudgmentSystem,
    judgmentCounts: dto.JudgmentCounts,
    inputCount: dto.InputCount,
    hitContextCount: dto.HitContextCount,
    floorCount: dto.FloorCount,
    inputBytes: dto.InputBytes,
    hitContextBytes: dto.HitContextBytes,
    hasMicrophoneRecording: dto.HasMicrophoneRecording,
    microphoneRecordingBytes: dto.MicrophoneRecordingBytes,
    microphoneDurationSeconds: dto.MicrophoneDurationSeconds,
    microphoneSampleRate: dto.MicrophoneSampleRate,
    microphoneChannels: dto.MicrophoneChannels,
    microphoneRecordingPermanent: dto.MicrophoneRecordingPermanent,
    microphoneRecordingExpiresAtUtc: dto.MicrophoneRecordingExpiresAtUtc,
    submissionRunId: dto.SubmissionRunId,
    replayPlayable: dto.ReplayPlayable,
    replayUnavailableReason: dto.ReplayUnavailableReason,
  };
}

export function mapActivityChart(dto: ActivityChartDto) {
  return {
    levelSessionId: dto.LevelSessionId,
    levelText: dto.LevelText,
    floorCount: dto.FloorCount,
  };
}
