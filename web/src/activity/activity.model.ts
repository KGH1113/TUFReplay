export interface ActivityLevelSessionOverview {
  Id: string;
  LogicalLevelId: string;
  LevelGroupId: string;
  AppSessionId: string;
  TufLevelId: number | null;
  Song: string | null;
  Author: string | null;
  Artist: string | null;
  OpenedAtUtc: string;
  ClosedAtUtc: string | null;
  FloorCount: number;
  RunCount: number;
  ClearRunCount: number;
  NoFailRunCount: number;
  FirstStartTile: number | null;
  LastStartTile: number | null;
  ChartAvailable: boolean;
}

export interface ActivityLogicalLevelOverview {
  Id: string;
  TufLevelId: number | null;
  Song: string | null;
  Author: string | null;
  Artist: string | null;
  FirstSeenAtUtc: string;
  LastSeenAtUtc: string;
  FloorCount: number;
  VisitCount: number;
  RunCount: number;
  ClearRunCount: number;
  NoFailRunCount: number;
  FirstStartTile: number | null;
  LastStartTile: number | null;
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

export interface ActivityRun {
  Id: string;
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
  XAccuracy: number | null;
  JudgmentDifficulty: JudgmentDifficulty | null;
  JudgmentCounts: ActivityJudgmentCounts;
  InputCount: number;
  HitContextCount: number;
  FloorCount: number;
  InputBytes: number;
  HitContextBytes: number;
  HasMicrophoneRecording: boolean;
  MicrophoneRecordingBytes: number;
  MicrophoneDurationSeconds: number | null;
  MicrophoneSampleRate: number | null;
  MicrophoneChannels: number | null;
  MicrophoneRecordingPermanent: boolean;
  MicrophoneRecordingExpiresAtUtc: string | null;
}

export interface MicrophoneRecordingDeleteResult {
  RunId: string;
  Deleted: boolean;
}

export interface ActivityRunDeleteResult {
  RunId: string;
  Deleted: boolean;
}

export interface MicrophoneRecordingKeepResult {
  RunId: string;
  Permanent: boolean;
}

export type JudgmentDifficulty = "Lenient" | "Normal" | "Strict";

export interface ActivityJudgmentCounts {
  Overload: number;
  TooEarly: number;
  Early: number;
  EarlyPerfect: number;
  Perfect: number;
  LatePerfect: number;
  Late: number;
  TooLate: number;
  Miss: number;
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

export type ReplayState =
  | "idle"
  | "preparing"
  | "opening_level"
  | "waiting_for_focus"
  | "starting"
  | "playing"
  | "returning_to_editor"
  | "completed"
  | "cancelled"
  | "error";

export interface ReplayStatus {
  OperationId: string | null;
  RunId: string | null;
  State: ReplayState;
  ErrorCode: string | null;
  Message: string | null;
}

export type ReplayLevelFilePickerOutcome =
  | "picking"
  | "selected"
  | "mismatch"
  | "cancelled"
  | "error";

export interface ReplayLevelFilePickerResult {
  OperationId: string | null;
  RunId: string;
  Outcome: ReplayLevelFilePickerOutcome;
  LevelPath: string | null;
  ErrorCode: string | null;
  Message: string | null;
}

export interface ActivityDay {
  date: string;
  appSessions: ActivityAppSession[];
  levelSessions: ActivityLevelCardOverview[];
  hasOpenableLevels: boolean;
  runCount: number;
  clearRunCount: number;
}

export interface ActivityLevelCardOverview extends ActivityLogicalLevelOverview {
  LevelGroupId: string;
  CanOpen: boolean;
  VisibleRunCount: number;
  HiddenRunCount: number;
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

export type ConnectionStatus = "connecting" | "online" | "incompatible" | "error";

export interface MicrophoneDevice {
  Id: string;
  Name: string;
  MinFrequency: number;
  MaxFrequency: number;
}

export interface MicrophoneDevicesState {
  Enabled: boolean;
  ToggleLocked: boolean;
  Devices: MicrophoneDevice[];
  SelectedDeviceId: string | null;
  MicrophoneOffsetMs: number;
  MicrophoneVolumeDb: number;
}

export interface MicrophoneTimingSettings {
  MicrophoneOffsetMs: number;
  MicrophoneVolumeDb: number;
}

export type MicrophoneCalibrationState =
  | "idle"
  | "arming"
  | "opening_level"
  | "waiting_for_run"
  | "recording"
  | "processing"
  | "editing"
  | "preview_starting"
  | "preview_playing"
  | "error";

export interface MicrophoneCalibrationStatus {
  OperationId: string | null;
  State: MicrophoneCalibrationState;
  ErrorCode: string | null;
  Message: string | null;
  DurationMs: number;
  PlaybackPositionMs: number;
  ResultRevision: number;
  MicrophoneOffsetMs: number;
  MicrophoneVolumeDb: number;
}

export interface MicrophoneCalibrationResult {
  OperationId: string;
  Revision: number;
  DurationMs: number;
  GameWaveform: number[];
  SongWaveform?: number[];
  MicrophoneWaveform: number[];
}

export interface MicrophoneOffsetCalibrationData {
  durationMs: number;
  songWaveform: number[];
  microphoneWaveform: number[];
}
