export interface ActivityDaySummary {
  Date: string;
  AppSessionCount: number;
  LevelSessionCount: number;
  RunCount: number;
  ClearRunCount: number;
  NoFailRunCount: number;
  UniqueLevelCount: number;
  StartedAtUtc: string;
  EndedAtUtc: string | null;
}

export interface ActivityLevelSession {
  Id: string;
  AppSessionId: string;
  TufLevelId: number;
  OpenedAtUtc: string;
  ClosedAtUtc: string | null;
  LevelTileCount: number;
  RunCount: number;
  NoFailRunCount: number;
  FirstStartTile: number | null;
  LastStartTile: number | null;
}

export interface ActivityAppSession {
  Id: string;
  StartedAtUtc: string;
  EndedAtUtc: string | null;
  LevelSessions: ActivityLevelSession[];
}

export interface ActivityDay {
  Date: string;
  Summary: ActivityDaySummary;
  AppSessions: ActivityAppSession[];
}

export interface ActivitySegmentGroup {
  SegmentGroupIndex: number;
  StartTile: number;
  AttemptCount: number;
  BestLastTile: number;
  FirstStartedAtUtc: string;
  LastStartedAtUtc: string;
}

export interface ActivityLevelSessionDetail {
  Session: ActivityLevelSession;
  SegmentGroups: ActivitySegmentGroup[];
}

export interface ActivityRun {
  Id: string;
  AppSessionId: string;
  LevelSessionId: string;
  TufLevelId: number;
  RunIndex: number;
  SegmentGroupIndex: number;
  StartedAtUtc: string;
  EndedAtUtc: string | null;
  LevelTileCount: number;
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

export interface LevelMetadata {
  Difficulty: string;
  DifficultyIconUrl: string;
  LevelId: number;
  Artist: string;
  Name: string;
  Creator: string;
}

export interface PlottedSegmentGroup {
  group: ActivitySegmentGroup;
  runs: ActivityRun[];
}

export interface TimeExpansionRange {
  start: number;
  end: number;
  extra: number;
}

export interface AdaptiveTimeScale {
  domain: { min: number; max: number };
  expandedRanges: TimeExpansionRange[];
  toPosition: (value: number) => number;
  toPercent: (value: number) => number;
}
