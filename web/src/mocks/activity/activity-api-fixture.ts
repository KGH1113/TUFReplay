import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLegacyReplayStatus,
  ActivityLevelSessionOverview,
  ActivityLogicalLevelOverview,
  ActivityRun,
  ActivityRunBatchDeleteResult,
  ActivityRunDeleteResult,
  ActivityRunLockResult,
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
import type {
  RunJudgmentTimeline,
  TimelineJudgmentKind,
} from "@/models/activity/judgment-timeline";
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
  getLegacyReplayStatus(): Promise<ActivityLegacyReplayStatus>;
  listAppSessions(offset: number, limit: number): Promise<ActivityAppSession[]>;
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
  deleteCameraRecording(runId: string): Promise<ActivityRunDeleteResult>;
  keepCameraRecording(runId: string): Promise<{ RunId: string; Permanent: boolean }>;
  changeRunLock(runId: string, locked: boolean): Promise<ActivityRunLockResult>;
  deleteAppSessionRuns(
    appSessionIds: string[],
    keepClears: boolean,
  ): Promise<ActivityRunBatchDeleteResult>;
  setCameraRetentionDays(days: number): boolean;
  judgmentTimelines(runIds: string[]): RunJudgmentTimeline[];
  assertCameraRecording(runId: string): void;
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

// Mirrors the recorder: No-Fail runs count when they had no Miss or Overload.
function isClearRun(run: ActivityRun) {
  const result = run.Result.toLowerCase();
  return (
    (result === "cleared" || result === "completed") &&
    run.StartTile === 0 &&
    (!run.NoFailMode || (run.JudgmentCounts.Miss === 0 && run.JudgmentCounts.Overload === 0))
  );
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

levels.push(createPracticeLevel());

const appSessions: ActivityAppSession[] = [
  createAppSession("app-2026-07-13", "2026-07-13T01:18:00.000Z"),
  createAppSession("app-2026-07-12", "2026-07-12T09:08:00.000Z", "2026-07-12T11:40:00.000Z"),
  createAppSession("app-2026-07-08", "2026-07-08T11:02:00.000Z", "2026-07-08T13:55:00.000Z"),
];

class MockActivityDomainError extends Error {
  readonly code: string;

  constructor(code: string, message: string) {
    super(message);
    this.name = "MockActivityDomainError";
    this.code = code;
  }
}

const activitySeed = { levels, appSessions };

export function createActivityWireFixture(): ActivityWireFixture {
  const { levels, appSessions } = structuredClone(activitySeed);
  const findLevel = (id: string): MockLevel => {
    const level = levels.find((candidate) => candidate.session.Id === id);
    if (!level) throw new Error(`Unknown mock level session: ${id}`);
    return level;
  };
  let cameraRetentionDays = 7;
  const keptCameraRuns = new Set<string>();
  const removeLevel = (index: number) => {
    const [level] = levels.splice(index, 1);
    const appIndex = appSessions.findIndex((session) => session.Id === level.session.AppSessionId);
    if (appIndex < 0) return;
    const appSession = appSessions[appIndex];
    appSession.LevelSessions = appSession.LevelSessions.filter(
      (session) => session.Id !== level.session.Id,
    );
    if (appSession.LevelSessions.length === 0 && appSession.EndedAtUtc)
      appSessions.splice(appIndex, 1);
  };
  const keepRecordings = (run: ActivityRun) => {
    if (run.HasMicrophoneRecording) {
      run.MicrophoneRecordingPermanent = true;
      run.MicrophoneRecordingExpiresAtUtc = null;
    }
    if (run.HasCameraRecording) {
      run.CameraRecordingPermanent = true;
      run.CameraRecordingExpiresAtUtc = null;
      keptCameraRuns.add(run.Id);
    }
  };
  const runLocked = () =>
    new MockActivityDomainError("run_locked", "Unlock this run to delete it.");
  const cameraRecordedAt = new Map(
    levels.flatMap((level) =>
      level.runs.map(
        (run) =>
          [
            run.Id,
            run.CameraRecordingExpiresAtUtc
              ? Date.parse(run.CameraRecordingExpiresAtUtc) - 7 * 86_400_000
              : Date.now(),
          ] as const,
      ),
    ),
  );
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
      Bluetooth: false,
    },
    {
      Id: "USB Audio Device",
      Name: "USB Audio Device",
      MinFrequency: 44_100,
      MaxFrequency: 48_000,
      Bluetooth: false,
    },
    {
      Id: "AirPods Pro",
      Name: "AirPods Pro",
      MinFrequency: 24_000,
      MaxFrequency: 24_000,
      Bluetooth: true,
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
    judgmentTimelines: (runIds) =>
      runIds.flatMap((runId) => {
        for (const level of levels) {
          const run = level.runs.find((candidate) => candidate.Id === runId);
          if (run) return [mockJudgmentTimeline(run)];
        }
        return [];
      }),
    assertCameraRecording: (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        if (!run.HasCameraRecording)
          throw new MockActivityDomainError(
            "camera_recording_not_found",
            "Camera recording was not found",
          );
        return;
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    setCameraRetentionDays: (days) => {
      if (days === cameraRetentionDays) return false;
      cameraRetentionDays = days;
      for (const level of levels)
        for (const run of level.runs) {
          if (!run.HasCameraRecording) continue;
          run.CameraRecordingPermanent = days === 0 || keptCameraRuns.has(run.Id);
          run.CameraRecordingExpiresAtUtc = run.CameraRecordingPermanent
            ? null
            : new Date(
                (cameraRecordedAt.get(run.Id) ?? Date.now()) + days * 86_400_000,
              ).toISOString();
        }
      return true;
    },
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
        RelativeLevelPath: level.session.RelativeLevelPath,
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
        if (level.runs[runIndex].Locked) throw runLocked();
        level.runs.splice(runIndex, 1);
        recountLevel(level);
        if (level.runs.length === 0) removeLevel(index);
        return { RunId: runId, Deleted: true };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    changeRunLock: async (runId, locked) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        run.Locked = locked;
        if (locked) keepRecordings(run);
        recountLevel(level);
        return { RunId: runId, Locked: locked };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    deleteAppSessionRuns: async (appSessionIds, keepClears) => {
      let deleted = 0;
      for (let index = levels.length - 1; index >= 0; index -= 1) {
        const level = levels[index];
        if (!appSessionIds.includes(level.session.AppSessionId)) continue;
        if (keepClears)
          for (const run of level.runs)
            if (isClearRun(run) && !run.Locked) {
              run.Locked = true;
              keepRecordings(run);
            }
        const kept = level.runs.filter((run) => run.Locked);
        deleted += level.runs.length - kept.length;
        level.runs = kept;
        recountLevel(level);
        if (kept.length === 0) removeLevel(index);
      }
      const keptLocked = levels
        .filter((level) => appSessionIds.includes(level.session.AppSessionId))
        .reduce((sum, level) => sum + level.session.LockedRunCount, 0);
      return {
        DeletedRunCount: deleted,
        KeptLockedRunCount: keptLocked,
        KeptInUseRunCount: 0,
      };
    },
    deleteMicrophoneRecording: async (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        if (run.Locked) throw runLocked();
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
    deleteCameraRecording: async (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        if (run.Locked) throw runLocked();
        const deleted = run.HasCameraRecording;
        run.HasCameraRecording = false;
        run.CameraRecordingBytes = 0;
        run.CameraRecordingPermanent = false;
        run.CameraRecordingExpiresAtUtc = null;
        return { RunId: runId, Deleted: deleted };
      }
      throw new MockActivityDomainError("run_not_found", "Run was not found");
    },
    keepCameraRecording: async (runId) => {
      for (const level of levels) {
        const run = level.runs.find((candidate) => candidate.Id === runId);
        if (!run) continue;
        if (!run.HasCameraRecording)
          throw new MockActivityDomainError(
            "camera_recording_not_found",
            "Camera recording was not found",
          );
        run.CameraRecordingPermanent = true;
        keptCameraRuns.add(runId);
        run.CameraRecordingExpiresAtUtc = null;
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
      DefaultDeviceBluetooth: false,
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
        DefaultDeviceBluetooth: false,
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
        DefaultDeviceBluetooth: false,
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

function createAppSession(
  id: string,
  startedAtUtc: string,
  endedAtUtc: string | null = null,
): ActivityAppSession {
  const levelSessions = levels
    .filter((level) => level.session.AppSessionId === id)
    .map((level) => level.session);
  return {
    Id: id,
    StartedAtUtc: startedAtUtc,
    EndedAtUtc: endedAtUtc,
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
      RelativeLevelPath: `${id}.adofai`,
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
      LockedRunCount: 0,
      LockedClearRunCount: 0,
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
  const hasCameraRecording = index % 3 !== 2;
  const expiresAt = new Date(Date.now() + 7 * 86_400_000).toISOString();
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
    MicrophoneRecordingExpiresAtUtc: hasMicrophoneRecording ? expiresAt : null,
    HasCameraRecording: hasCameraRecording,
    CameraRecordingBytes: hasCameraRecording ? (4 + index) * 1_048_576 : 0,
    CameraRecordingPermanent: false,
    CameraRecordingExpiresAtUtc: hasCameraRecording ? expiresAt : null,
    ReplayPlayable: true,
    Locked: false,
    ReplayUnavailableReason: null,
  };
}

/**
 * A long practice session: many attempts from several checkpoints, a few clears, locked and
 * unplayable runs, mixed pitch and competitive judgments, so busy days can be previewed.
 */
function createPracticeLevel(): MockLevel {
  const level = createLevel(
    "level-871-practice",
    "app-2026-07-08",
    871,
    "2026-07-08T11:05:00.000Z",
    level871Text,
    [],
  );
  const floorCount = level.session.FloorCount;
  const starts = [0, 0, 0, 214, 214, 356, 356, 356, 480, 0, 214, 356, 0, 0, 480, 480];
  level.runs = Array.from({ length: 64 }, (_, index) => {
    const startTile = starts[index % starts.length];
    const run = createRun(
      level.session.Id,
      871,
      level.session.OpenedAtUtc,
      floorCount,
      startTile,
      index,
    );
    const cleared = startTile === 0 && (index === 24 || index === 51 || index === 60);
    const reach = Math.min(
      floorCount - 1,
      startTile + 20 + ((index * 97) % Math.max(1, floorCount - startTile - 20)),
    );
    run.LastTile = cleared ? floorCount - 1 : reach;
    run.Result = cleared ? "Cleared" : index % 11 === 7 ? "Aborted" : "Failed";
    run.StartedAtUtc = new Date(
      Date.parse(level.session.OpenedAtUtc) + index * 150_000,
    ).toISOString();
    run.EndedAtUtc = new Date(
      Date.parse(run.StartedAtUtc) + 20_000 + ((run.LastTile - startTile) / floorCount) * 180_000,
    ).toISOString();
    run.XAccuracy = cleared ? 0.9912 - index * 0.0001 : 0.93 + ((index * 37) % 60) / 1000;
    run.LevelPitchPercent = index % 9 === 4 ? 110 : index % 13 === 6 ? 90 : 100;
    run.JudgmentSystem = index >= 40 ? "ModernCompetitive" : "ModernClassic";
    run.JudgmentCounts.Perfect = Math.max(1, run.LastTile - startTile - 12);
    if (run.JudgmentSystem === "ModernCompetitive") {
      run.JudgmentCounts.XPerfect = Math.round(run.JudgmentCounts.Perfect * 0.7);
      run.JudgmentCounts.PerfectMinus = Math.round(run.JudgmentCounts.Perfect * 0.16);
      run.JudgmentCounts.PerfectPlus =
        run.JudgmentCounts.Perfect - run.JudgmentCounts.XPerfect - run.JudgmentCounts.PerfectMinus;
    }
    run.JudgmentCounts.Miss = cleared ? 0 : 1;
    run.NoFailMode = index === 33;
    run.Locked = cleared || index === 17;
    if (run.Locked) {
      run.MicrophoneRecordingPermanent = run.HasMicrophoneRecording;
      run.MicrophoneRecordingExpiresAtUtc = null;
      run.CameraRecordingPermanent = run.HasCameraRecording;
      run.CameraRecordingExpiresAtUtc = null;
    }
    if (index === 9) {
      run.ReplayPlayable = false;
      run.ReplayUnavailableReason = "input_queue_overflow";
    }
    return run;
  });
  recountLevel(level);
  level.session.FirstStartTile = 0;
  level.session.LastStartTile = 480;
  level.session.ClosedAtUtc = "2026-07-08T13:50:00.000Z";
  level.session.RelativeLevelPath = "871 Xtrullor - Cry/level.adofai";
  // A separate download folder, so this practice copy stays openable next to the newer visit.
  level.session.LevelGroupId = "mock-tuf-871-practice";
  return level;
}

const timelineKinds = [
  "Overload",
  "TooEarly",
  "Early",
  "EarlyPerfect",
  "LatePerfect",
  "Late",
  "TooLate",
  "Miss",
] as const satisfies readonly TimelineJudgmentKind[];

/**
 * Spreads a run's non-perfect counts across its length with a seeded generator, so each run
 * has a stable, believable timeline; a failed run's miss lands at the point it ended.
 */
function mockJudgmentTimeline(run: ActivityRun): RunJudgmentTimeline {
  const durationMs = Math.max(
    1_000,
    Date.parse(run.EndedAtUtc ?? run.StartedAtUtc) - Date.parse(run.StartedAtUtc),
  );
  let seed = [...run.Id].reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) >>> 0, 7);
  const random = () => {
    seed = (seed * 1_664_525 + 1_013_904_223) >>> 0;
    return seed / 2 ** 32;
  };
  const events: { timeMs: number; kind: TimelineJudgmentKind }[] = [];
  for (const kind of timelineKinds) {
    const count = Math.min(400, run.JudgmentCounts[kind] ?? 0);
    for (let index = 0; index < count; index++) {
      const failed =
        kind === "Miss" && run.Result.toLowerCase() === "failed" && index === count - 1;
      // Mistakes cluster around a few hard sections instead of spreading evenly.
      const section = Math.floor(random() * 4) / 4 + 0.08;
      const timeMs = failed
        ? durationMs - 200
        : Math.round(Math.min(0.99, section + random() * 0.18) * durationMs);
      events.push({ timeMs, kind });
    }
  }
  events.sort((left, right) => left.timeMs - right.timeMs);
  return {
    runId: run.Id,
    durationMs,
    timesMs: events.map((event) => event.timeMs),
    kinds: events.map((event) => event.kind),
  };
}

function recountLevel(level: MockLevel) {
  const runs = level.runs;
  level.session.RunCount = runs.length;
  level.session.ClearRunCount = runs.filter(isClearRun).length;
  level.session.NoFailRunCount = runs.filter((run) => run.NoFailMode).length;
  level.session.LockedRunCount = runs.filter((run) => run.Locked).length;
  level.session.LockedClearRunCount = runs.filter((run) => run.Locked && isClearRun(run)).length;
}

function readFloorCount(levelText: string): number {
  const pathData = levelText.match(/"pathData"\s*:\s*"([^"]*)"/)?.[1];
  if (pathData) return pathData.length;
  const angleData = levelText.match(/"angleData"\s*:\s*\[([^\]]*)\]/)?.[1];
  return angleData ? angleData.split(",").length : 0;
}
