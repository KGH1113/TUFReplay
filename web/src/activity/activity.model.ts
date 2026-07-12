export interface ActivityLevelSessionOverview {
  Id: string;
  AppSessionId: string;
  TufLevelId: number | null;
  OpenedAtUtc: string;
  ClosedAtUtc: string | null;
  RunCount: number;
  ClearRunCount: number;
  ChartAvailable: boolean;
}

export interface ActivityAppSession {
  Id: string;
  StartedAtUtc: string;
  EndedAtUtc: string | null;
  RecorderTimeZoneId: string | null;
  RecorderUtcOffsetMinutes: number;
  LevelSessions: ActivityLevelSessionOverview[];
}

export interface ActivityLevelSessionDetail extends ActivityLevelSessionOverview {
  AppSession: ActivityAppSession;
}

export interface ActivityRun {
  Id: string;
  AppSessionId: string;
  LevelSessionId: string;
  TufLevelId: number | null;
  RunIndex: number;
  StartedAtUtc: string;
  EndedAtUtc: string | null;
  StartTile: number;
  LastTile: number | null;
  Result: string;
  NoFailMode: boolean;
  GameplayStartSongPosition: number | null;
  LevelPitchPercent: number | null;
  EffectivePitch: number | null;
  InputCount: number;
  HitContextCount: number;
  Meta: unknown;
}

export interface ActivityChart {
  LevelSessionId: string;
  LevelText: string;
  FloorCount: number;
}

export interface RunMarker {
  id: string;
  floorIndex: number;
  count: number;
  clearCount: number;
  bestLastFloorIndex: number;
}

export interface ActivityDay {
  date: string;
  appSessions: ActivityAppSession[];
  levelSessions: ActivityLevelSessionOverview[];
  runCount: number;
  clearRunCount: number;
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

export type ConnectionStatus = "connecting" | "online" | "error";
