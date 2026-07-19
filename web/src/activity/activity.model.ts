export interface ActivityLevelSessionOverview {
  Id: string;
  AppSessionId: string;
  TufLevelId: number | null;
  Song: string | null;
  Author: string | null;
  Artist: string | null;
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

export type ReplayLevelFilePickerState = "picking" | "selected" | "cancelled" | "error";

export interface ReplayLevelFilePickerStatus {
  OperationId: string;
  RunId: string;
  State: ReplayLevelFilePickerState;
  LevelPath: string | null;
  ErrorCode: string | null;
  Message: string | null;
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

export interface MicrophoneDevice {
  Id: string;
  Name: string;
  MinFrequency: number;
  MaxFrequency: number;
}

export interface MicrophoneDevicesState {
  Devices: MicrophoneDevice[];
  SelectedDeviceId: string | null;
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
  MicrophoneWaveform: number[];
}

export interface MicrophoneOffsetCalibrationData {
  durationMs: number;
  gameWaveform: number[];
  microphoneWaveform: number[];
}
