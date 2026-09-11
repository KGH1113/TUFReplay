import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLegacyReplayStatus,
  ActivityLevelSessionOverview,
  ActivityLogicalLevelOverview,
  ActivityRun,
  ActivityRunDeleteResult,
  MicrophoneCalibrationResult,
  MicrophoneCalibrationStatus,
  MicrophoneDevicesState,
  MicrophoneRecordingDeleteResult,
  MicrophoneRecordingKeepResult,
  MicrophoneTimingSettings,
  ReplayLevelFilePickerResult,
  ReplayStatus,
} from "@/mocks/activity/activity-wire-fixture";
import { mockMicrophoneOffsetCalibration } from "@/mocks/calibration/microphone-offset-fixture";
import level5Text from "./levels/tuf-5.adofai?raw";
import level303Text from "./levels/tuf-303.adofai?raw";
import level871Text from "./levels/tuf-871.adofai?raw";

interface MockLevel {
  session: ActivityLevelSessionOverview;
  chart: ActivityChart;
  runs: ActivityRun[];
}

interface ActivityWireFixture {
  health(): Promise<{
    Ok: boolean;
    Mod: string;
    ModVersion: string;
    ProtocolVersion: number;
    ServerVersion: number;
  }>;
  listAppSessions(offset: number, limit: number): Promise<ActivityAppSession[]>;
  getLegacyReplayStatus(): Promise<ActivityLegacyReplayStatus>;
  listAllAppSessions(onPage?: (items: ActivityAppSession[]) => void): Promise<ActivityAppSession[]>;
  getLevelSession(id: string): Promise<ActivityLevelSessionOverview>;
  getLogicalLevel(id: string): Promise<ActivityLogicalLevelOverview>;
  listAllRuns(id: string, onPage?: (items: ActivityRun[]) => void): Promise<ActivityRun[]>;
  listAllLogicalLevelRuns(
    id: string,
    appSessionIds: string[],
    onPage?: (items: ActivityRun[]) => void,
  ): Promise<ActivityRun[]>;
  getChart(id: string): Promise<ActivityChart>;
  getLogicalLevelChart(id: string): Promise<ActivityChart>;
  deleteRun(runId: string): Promise<ActivityRunDeleteResult>;
  deleteMicrophoneRecording(runId: string): Promise<MicrophoneRecordingDeleteResult>;
  keepMicrophoneRecording(runId: string): Promise<MicrophoneRecordingKeepResult>;
  playReplay(runId: string, levelPath?: string): Promise<ReplayStatus>;
  getReplayStatus(): Promise<ReplayStatus>;
  pickReplayLevelFile(runId: string): Promise<ReplayLevelFilePickerResult>;
  getMicrophoneDevices(): Promise<MicrophoneDevicesState>;
  setMicrophoneEnabled(enabled: boolean): Promise<MicrophoneDevicesState>;
  selectMicrophoneDevice(deviceId: string | null): Promise<MicrophoneDevicesState>;
  setMicrophoneOffset(offsetMs: number): Promise<MicrophoneTimingSettings>;
  setMicrophoneVolume(volumeDb: number): Promise<MicrophoneTimingSettings>;
  startMicrophoneCalibration(): Promise<MicrophoneCalibrationStatus>;
  getMicrophoneCalibrationStatus(operationId: string): Promise<MicrophoneCalibrationStatus>;
  getMicrophoneCalibrationResult(
    operationId: string,
    revision: number,
  ): Promise<MicrophoneCalibrationResult>;
  playMicrophoneCalibrationPreview(operationId: string): Promise<MicrophoneCalibrationStatus>;
  stopMicrophoneCalibrationPreview(operationId: string): Promise<MicrophoneCalibrationStatus>;
  setMicrophoneCalibrationOffset(
    operationId: string,
    offsetMs: number,
  ): Promise<MicrophoneCalibrationStatus>;
  setMicrophoneCalibrationVolume(
    operationId: string,
    volumeDb: number,
  ): Promise<MicrophoneCalibrationStatus>;
  closeMicrophoneCalibration(operationId: string): Promise<MicrophoneCalibrationStatus>;
}

function isClearRun(run: ActivityRun) {
  const result = run.Result.toLowerCase();
  return (result === "cleared" || result === "completed") && run.StartTile === 0 && !run.NoFailMode;
}

const levels = [
  createLevel(
    "level-5",
    "app-2026-07-12",
    5,
    "2026-07-12T09:12:00.000Z",
    level5Text,
    [0, 0, 118, 245, 245, 402],
  ),
  createLevel(
    "level-303",
    "app-2026-07-12",
    303,
    "2026-07-12T10:03:00.000Z",
    level303Text,
    [0, 64, 64, 173, 288],
  ),
  createLevel(
    "level-871",
    "app-2026-07-13",
    871,
    "2026-07-13T01:20:00.000Z",
    level871Text,
    [0, 0, 92, 214, 356, 356, 480],
  ),
];

const appSessions: ActivityAppSession[] = [
  createAppSession("app-2026-07-13", "2026-07-13T01:18:00.000Z"),
  createAppSession("app-2026-07-12", "2026-07-12T09:08:00.000Z"),
];

class MockActivityDomainError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = "MockActivityDomainError";
    this.code = code;
  }
}

export function createActivityWireFixture(): ActivityWireFixture {
  let selectedMicrophoneDeviceId: string | null = null;
  let microphoneEnabled = true;
  let microphoneOffsetMs = mockMicrophoneOffsetCalibration.initialOffsetMs;
  let microphoneVolumeDb = 0;
  const microphoneDevices = [
    {
      Id: "MacBook Pro Microphone",
      Name: "MacBook Pro Microphone",
      MinFrequency: 48_000,
      MaxFrequency: 48_000,
    },
    {
      Id: "USB Audio Device",
      Name: "USB Audio Device",
      MinFrequency: 44_100,
      MaxFrequency: 48_000,
    },
  ];
  let replayStatus: ReplayStatus = {
    OperationId: null,
    RunId: null,
    State: "idle",
    ErrorCode: null,
    Message: null,
  };
  let calibrationStatus: MicrophoneCalibrationStatus = {
    OperationId: null,
    State: "idle",
    ErrorCode: null,
    Message: null,
    DurationMs: 0,
    PlaybackPositionMs: 0,
    ResultRevision: 0,
    MicrophoneOffsetMs: mockMicrophoneOffsetCalibration.initialOffsetMs,
    MicrophoneVolumeDb: 0,
  };
  return {
    health: async () => ({
      Ok: true,
      Mod: "TUFReplay",
      ModVersion: "mock",
      ProtocolVersion: 7,
      ServerVersion: 1,
      ReplayEngineId: "tufreplay.replay.v2",
      ReplayFormatVersion: 1,
    }),
    getLegacyReplayStatus: async () => ({ HasLegacyReplays: true }),
    listAppSessions: async (offset, limit) => appSessions.slice(offset, offset + limit),
    listAllAppSessions: async (onPage) => {
      onPage?.(appSessions);
      return appSessions;
    },
    getLevelSession: async (id) => findLevel(id).session,
    getLogicalLevel: async (id) => {
      const level = findLevel(id);
      return {
        Id: id,
        TufLevelId: level.session.TufLevelId,
        Song: level.session.Song,
        Author: level.session.Author,
        Artist: level.session.Artist,
        FirstSeenAtUtc: level.session.OpenedAtUtc,
        LastSeenAtUtc: level.session.ClosedAtUtc ?? level.session.OpenedAtUtc,
        FloorCount: level.session.FloorCount,
        VisitCount: 1,
        RunCount: level.session.RunCount,
        ClearRunCount: level.session.ClearRunCount,
        NoFailRunCount: level.session.NoFailRunCount,
        FirstStartTile: level.session.FirstStartTile,
        LastStartTile: level.session.LastStartTile,
        ChartAvailable: level.session.ChartAvailable,
      };
    },
    listAllRuns: async (id, onPage) => {
      const runs = findLevel(id).runs;
      onPage?.(runs);
      return runs;
    },
    getChart: async (id) => findLevel(id).chart,
    listAllLogicalLevelRuns: async (id, appSessionIds, onPage) => {
      const level = findLevel(id);
      const runs = appSessionIds.includes(level.session.AppSessionId) ? level.runs : [];
      onPage?.(runs);
      return runs;
    },
    getLogicalLevelChart: async (id) => findLevel(id).chart,
    deleteRun: async (runId) => {
      for (let index = levels.length - 1; index >= 0; index -= 1) {
        const level = levels[index];
        const runIndex = level.runs.findIndex((candidate) => candidate.Id === runId);
        if (runIndex < 0) continue;
        level.runs.splice(runIndex, 1);
        level.session.RunCount = level.runs.length;
        level.session.ClearRunCount = level.runs.filter((run) => run.Result === "clear").length;
        level.session.NoFailRunCount = level.runs.filter((run) => run.NoFailMode).length;
        if (level.runs.length === 0) {
          levels.splice(index, 1);
          const appIndex = appSessions.findIndex(
            (session) => session.Id === level.session.AppSessionId,
          );
          if (appIndex >= 0) {
            const appSession = appSessions[appIndex];
            appSession.LevelSessions = appSession.LevelSessions.filter(
              (session) => session.Id !== level.session.Id,
            );
            if (appSession.LevelSessions.length === 0 && appSession.EndedAtUtc)
              appSessions.splice(appIndex, 1);
          }
        }
        return { RunId: runId, Deleted: true };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    deleteMicrophoneRecording: async (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        const deleted = run.HasMicrophoneRecording;
        run.HasMicrophoneRecording = false;
        run.MicrophoneRecordingBytes = 0;
        run.MicrophoneDurationSeconds = null;
        run.MicrophoneRecordingPermanent = false;
        run.MicrophoneRecordingExpiresAtUtc = null;
        return { RunId: runId, Deleted: deleted };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    keepMicrophoneRecording: async (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        if (!run.HasMicrophoneRecording)
          throw new MockActivityDomainError(
            "microphone_recording_not_found",
            "Microphone recording was not found",
          );
        run.MicrophoneRecordingPermanent = true;
        run.MicrophoneRecordingExpiresAtUtc = null;
        return { RunId: runId, Permanent: true };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    playReplay: async (runId) => {
      replayStatus = {
        OperationId: `mock-${runId}`,
        RunId: runId,
        State: "playing",
        ErrorCode: null,
        Message: null,
      };
      return replayStatus;
    },
    getReplayStatus: async () => replayStatus,
    pickReplayLevelFile: async (runId): Promise<ReplayLevelFilePickerResult> => {
      return {
        OperationId: null,
        RunId: runId,
        Outcome: "selected",
        LevelPath: `/mock/${runId}.adofai`,
        ErrorCode: null,
        Message: "Matching level file selected.",
      };
    },
    getMicrophoneDevices: async () => ({
      Enabled: microphoneEnabled,
      ToggleLocked: false,
      Devices: microphoneEnabled ? microphoneDevices : [],
      SelectedDeviceId: selectedMicrophoneDeviceId,
      MicrophoneOffsetMs: microphoneOffsetMs,
      MicrophoneVolumeDb: microphoneVolumeDb,
    }),
    setMicrophoneEnabled: async (enabled) => {
      microphoneEnabled = enabled;
      return {
        Enabled: microphoneEnabled,
        ToggleLocked: false,
        Devices: microphoneEnabled ? microphoneDevices : [],
        SelectedDeviceId: selectedMicrophoneDeviceId,
        MicrophoneOffsetMs: microphoneOffsetMs,
        MicrophoneVolumeDb: microphoneVolumeDb,
      };
    },
    selectMicrophoneDevice: async (deviceId) => {
      selectedMicrophoneDeviceId = deviceId;
      return {
        Enabled: microphoneEnabled,
        ToggleLocked: false,
        Devices: microphoneDevices,
        SelectedDeviceId: selectedMicrophoneDeviceId,
        MicrophoneOffsetMs: microphoneOffsetMs,
        MicrophoneVolumeDb: microphoneVolumeDb,
      };
    },
    setMicrophoneOffset: async (offsetMs) => {
      microphoneOffsetMs = offsetMs;
      return {
        MicrophoneOffsetMs: microphoneOffsetMs,
        MicrophoneVolumeDb: microphoneVolumeDb,
      };
    },
    setMicrophoneVolume: async (volumeDb) => {
      microphoneVolumeDb = volumeDb;
      return {
        MicrophoneOffsetMs: microphoneOffsetMs,
        MicrophoneVolumeDb: microphoneVolumeDb,
      };
    },
    startMicrophoneCalibration: async () => {
      calibrationStatus = {
        ...calibrationStatus,
        OperationId: "mock-calibration",
        State: "arming",
        Message: "Preparing microphone access.",
      };
      return calibrationStatus;
    },
    getMicrophoneCalibrationStatus: async () => calibrationStatus,
    getMicrophoneCalibrationResult: async (): Promise<MicrophoneCalibrationResult> => ({
      OperationId: "mock-calibration",
      Revision: 1,
      DurationMs: mockMicrophoneOffsetCalibration.durationMs,
      GameWaveform: mockMicrophoneOffsetCalibration.gameWaveform,
      SongWaveform: mockMicrophoneOffsetCalibration.songWaveform,
      MicrophoneWaveform: mockMicrophoneOffsetCalibration.microphoneWaveform,
    }),
    playMicrophoneCalibrationPreview: async () => {
      calibrationStatus = { ...calibrationStatus, State: "preview_playing" };
      return calibrationStatus;
    },
    stopMicrophoneCalibrationPreview: async () => {
      calibrationStatus = { ...calibrationStatus, State: "editing", PlaybackPositionMs: 0 };
      return calibrationStatus;
    },
    setMicrophoneCalibrationOffset: async (_operationId, offsetMs) => {
      calibrationStatus = { ...calibrationStatus, MicrophoneOffsetMs: offsetMs };
      return calibrationStatus;
    },
    setMicrophoneCalibrationVolume: async (_operationId, volumeDb) => {
      calibrationStatus = { ...calibrationStatus, MicrophoneVolumeDb: volumeDb };
      return calibrationStatus;
    },
    closeMicrophoneCalibration: async () => {
      calibrationStatus = { ...calibrationStatus, OperationId: null, State: "idle" };
      return calibrationStatus;
    },
  };
}

function createAppSession(id: string, startedAtUtc: string): ActivityAppSession {
  const levelSessions = levels
    .filter((level) => level.session.AppSessionId === id)
    .map((level) => level.session);
  return {
    Id: id,
    StartedAtUtc: startedAtUtc,
    EndedAtUtc: null,
    RecorderTimeZoneId: "Asia/Seoul",
    RecorderUtcOffsetMinutes: 540,
    LevelSessions: levelSessions,
  };
}

function createLevel(
  id: string,
  appSessionId: string,
  tufLevelId: number,
  openedAtUtc: string,
  levelText: string,
  starts: number[],
): MockLevel {
  const floorCount = readFloorCount(levelText);
  const runs = starts.map((startTile, index) =>
    createRun(id, tufLevelId, openedAtUtc, floorCount, startTile, index),
  );
  const clearRunCount = runs.filter(isClearRun).length;
  return {
    session: {
      Id: id,
      LogicalLevelId: id,
      LevelGroupId: `mock-tuf-${tufLevelId}`,
      AppSessionId: appSessionId,
      TufLevelId: tufLevelId,
      Song: null,
      Author: null,
      Artist: null,
      OpenedAtUtc: openedAtUtc,
      ClosedAtUtc: null,
      FloorCount: floorCount,
      RunCount: runs.length,
      ClearRunCount: clearRunCount,
      NoFailRunCount: runs.filter((run) => run.NoFailMode).length,
      FirstStartTile: Math.min(...starts),
      LastStartTile: Math.max(...starts),
      ChartAvailable: true,
    },
    chart: { LevelSessionId: id, LevelText: levelText, FloorCount: floorCount },
    runs,
  };
}

function createRun(
  levelSessionId: string,
  tufLevelId: number,
  openedAtUtc: string,
  floorCount: number,
  startTile: number,
  index: number,
): ActivityRun {
  const startedAt = new Date(new Date(openedAtUtc).getTime() + (index + 1) * 75_000);
  const cleared = index === 0 && startTile === 0;
  const lastTile = cleared ? floorCount - 1 : Math.min(floorCount - 1, startTile + 35 + index * 19);
  const hasMicrophoneRecording = index % 2 === 0;
  const microphoneDurationSeconds = 42 + index * 6;
  return {
    Id: `${levelSessionId}-run-${index + 1}`,
    LevelSessionId: levelSessionId,
    TufLevelId: tufLevelId,
    RunIndex: index + 1,
    StartedAtUtc: startedAt.toISOString(),
    EndedAtUtc: new Date(startedAt.getTime() + 42_000 + index * 6_000).toISOString(),
    StartTile: startTile,
    LastTile: lastTile,
    Result: cleared ? "Cleared" : "Failed",
    NoFailMode: tufLevelId === 5 && index === 1,
    GameplayStartSongPosition: null,
    LevelPitchPercent: 100,
    EffectivePitch: 1,
    XAccuracy: cleared ? 1 : Math.max(0, 0.985 - index * 0.011),
    JudgmentDifficulty: (["Strict", "Normal", "Lenient"] as const)[index % 3],
    JudgmentSystem: "ModernClassic",
    JudgmentCounts: {
      Overload: cleared ? 0 : index % 2,
      TooEarly: index,
      Early: index + 1,
      EarlyPerfect: 3 + index,
      Perfect: Math.max(1, lastTile - startTile - 8),
      PerfectMinus: 0,
      XPerfect: 0,
      PerfectPlus: 0,
      LatePerfect: 2 + index,
      Late: index,
      TooLate: cleared ? 0 : index % 3,
      Miss: cleared ? 0 : 1,
    },
    InputCount: Math.max(1, lastTile - startTile),
    HitContextCount: Math.max(1, lastTile - startTile),
    FloorCount: floorCount,
    InputBytes: 0,
    HitContextBytes: 0,
    HasMicrophoneRecording: hasMicrophoneRecording,
    MicrophoneRecordingBytes: hasMicrophoneRecording
      ? microphoneDurationSeconds * 48_000 * 2 + 44
      : 0,
    MicrophoneDurationSeconds: hasMicrophoneRecording ? microphoneDurationSeconds : null,
    MicrophoneSampleRate: hasMicrophoneRecording ? 48_000 : null,
    MicrophoneChannels: hasMicrophoneRecording ? 1 : null,
    MicrophoneRecordingPermanent: false,
    MicrophoneRecordingExpiresAtUtc: hasMicrophoneRecording ? "2026-07-27T12:00:00.000Z" : null,
    SubmissionRunId: null,
    ReplayPlayable: true,
    ReplayUnavailableReason: null,
  };
}

function readFloorCount(levelText: string): number {
  const pathData = levelText.match(/"pathData"\s*:\s*"([^"]*)"/)?.[1];
  if (pathData) return pathData.length;
  const angleData = levelText.match(/"angleData"\s*:\s*\[([^\]]*)\]/)?.[1];
  return angleData ? angleData.split(",").length : 0;
}

function findLevel(id: string): MockLevel {
  const level = levels.find((candidate) => candidate.session.Id === id);
  if (!level) throw new Error(`Unknown mock level session: ${id}`);
  return level;
}
