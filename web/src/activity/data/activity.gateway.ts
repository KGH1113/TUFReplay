import { AdofaiIpcClient, type AdofaiIpcNamespaceClient, tryConnect } from "@adofai-ipc/client";

import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLevelSessionOverview,
  ActivityLogicalLevelOverview,
  ActivityRun,
  ActivityRunDeleteResult,
  MicrophoneCalibrationResult,
  MicrophoneCalibrationStatus,
  MicrophoneDevicesState,
  MicrophoneRecordingDeleteResult,
  MicrophoneRecordingKeepResult,
  ReplayLevelFilePickerResult,
  ReplayStatus,
} from "../activity.model";
import { adofaiIpcFetch } from "./adofai-ipc.fetch";

const NAMESPACE = "tuf-replay";
const PAGE_SIZE = 200;
const FILE_PICKER_POLL_INTERVAL_MS = 100;
export const SUPPORTED_PROTOCOL_VERSION = 4;

export interface ActivityHealth {
  Ok: boolean;
  Mod: string;
  ModVersion: string;
  ProtocolVersion: number;
  ServerVersion: number;
}

interface DomainErrorPayload {
  error: {
    code: string;
    message: string;
  };
}

export class ActivityDomainError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = "ActivityDomainError";
  }
}

export class ActivityProtocolMismatchError extends Error {
  constructor(
    readonly expectedVersion: number,
    readonly detectedVersion: number | null,
    readonly modVersion: string | null,
  ) {
    const detected = detectedVersion === null ? "unknown" : String(detectedVersion);
    const mod = modVersion ?? "unknown";
    super(
      `TUFReplay IPC protocol mismatch (expected ${expectedVersion}, detected ${detected}, mod ${mod}).`,
    );
    this.name = "ActivityProtocolMismatchError";
  }
}

export interface ActivityGateway {
  health(): Promise<ActivityHealth>;
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
  playReplay(runId: string, levelPath?: string): Promise<ReplayStatus>;
  getReplayStatus(): Promise<ReplayStatus>;
  pickReplayLevelFile(runId: string): Promise<ReplayLevelFilePickerResult>;
  getMicrophoneDevices(): Promise<MicrophoneDevicesState>;
  setMicrophoneEnabled(enabled: boolean): Promise<MicrophoneDevicesState>;
  selectMicrophoneDevice(deviceId: string | null): Promise<MicrophoneDevicesState>;
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

export async function connectActivityGateway(): Promise<ActivityGateway> {
  const client = await tryConnect({
    fetch: adofaiIpcFetch,
  });
  const pickerClient = new AdofaiIpcClient({ baseUrl: client.baseUrl, fetch: adofaiIpcFetch });
  return createActivityGateway(client.namespace(NAMESPACE), pickerClient.namespace(NAMESPACE));
}

export function createActivityGateway(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
  pickerNamespace: Pick<AdofaiIpcNamespaceClient, "call"> = namespace,
): ActivityGateway {
  const listAppSessions = (offset: number, limit: number) =>
    callDomain<ActivityAppSession[]>(namespace, "activity.app-sessions.list", { offset, limit });

  return {
    health: async () => validateActivityHealth(await callDomain(namespace, "health.get", {})),
    listAppSessions,
    listAllAppSessions: (onPage) =>
      loadAllPages<ActivityAppSession>(listAppSessions, onPage),
    getLevelSession: (id) => callDomain(namespace, "activity.level-session.get", { id }),
    getLogicalLevel: (id) => callDomain(namespace, "activity.logical-level.get", { id }),
    listAllRuns: (id, onPage) =>
      loadAllPages<ActivityRun>(
        (offset, limit) =>
          callDomain<ActivityRun[]>(namespace, "activity.level-session.runs.list", {
            id,
            offset,
            limit,
          }),
        onPage,
      ),
    getChart: (id) => callDomain(namespace, "activity.level-session.chart.get", { id }),
    listAllLogicalLevelRuns: (id, appSessionIds, onPage) =>
      loadAllPages<ActivityRun>(
        (offset, limit) =>
          callDomain<ActivityRun[]>(namespace, "activity.logical-level.runs.list", {
            id,
            appSessionIds,
            offset,
            limit,
          }),
        onPage,
      ),
    getLogicalLevelChart: (id) => callDomain(namespace, "activity.logical-level.chart.get", { id }),
    deleteRun: (runId) => callDomain(namespace, "activity.run.delete", { runId }),
    deleteMicrophoneRecording: (runId) =>
      callDomain(namespace, "microphone.recording.delete", { runId }),
    keepMicrophoneRecording: (runId) =>
      callDomain(namespace, "microphone.recording.keep", { runId }),
    playReplay: (runId, levelPath) =>
      callDomain(namespace, "replay.play", levelPath ? { runId, levelPath } : { runId }),
    getReplayStatus: () => callDomain(namespace, "replay.status.get", {}),
    pickReplayLevelFile: async (runId) => {
      let result = await callDomain<ReplayLevelFilePickerResult>(
        pickerNamespace,
        "replay.level-file.pick",
        { runId },
      );
      while (result.Outcome === "picking" && result.OperationId) {
        await delay(FILE_PICKER_POLL_INTERVAL_MS);
        result = await callDomain<ReplayLevelFilePickerResult>(
          pickerNamespace,
          "replay.level-file.status.get",
          { operationId: result.OperationId },
        );
      }
      return result;
    },
    getMicrophoneDevices: () => callDomain(namespace, "microphone.devices.get", {}),
    setMicrophoneEnabled: (enabled) => callDomain(namespace, "microphone.enabled.set", { enabled }),
    selectMicrophoneDevice: (deviceId) =>
      callDomain(namespace, "microphone.device.select", { deviceId }),
    startMicrophoneCalibration: () => callDomain(namespace, "microphone.calibration.start", {}),
    getMicrophoneCalibrationStatus: (operationId) =>
      callDomain(namespace, "microphone.calibration.status.get", { operationId }),
    getMicrophoneCalibrationResult: (operationId, revision) =>
      callDomain(namespace, "microphone.calibration.result.get", { operationId, revision }),
    playMicrophoneCalibrationPreview: (operationId) =>
      callDomain(namespace, "microphone.calibration.preview.play", { operationId }),
    stopMicrophoneCalibrationPreview: (operationId) =>
      callDomain(namespace, "microphone.calibration.preview.stop", { operationId }),
    setMicrophoneCalibrationOffset: (operationId, offsetMs) =>
      callDomain(namespace, "microphone.calibration.offset.set", { operationId, offsetMs }),
    setMicrophoneCalibrationVolume: (operationId, volumeDb) =>
      callDomain(namespace, "microphone.calibration.volume.set", { operationId, volumeDb }),
    closeMicrophoneCalibration: (operationId) =>
      callDomain(namespace, "microphone.calibration.close", { operationId }),
  };
}

function delay(milliseconds: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, milliseconds));
}

export async function loadAllPages<T>(
  load: (offset: number, limit: number) => Promise<T[]>,
  onPage?: (items: T[]) => void,
): Promise<T[]> {
  const all: T[] = [];
  for (let offset = 0; ; offset += PAGE_SIZE) {
    const page = await load(offset, PAGE_SIZE);
    all.push(...page);
    onPage?.([...all]);
    if (page.length < PAGE_SIZE) return all;
  }
}

async function callDomain<TResult>(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
  method: string,
  params: object,
): Promise<TResult> {
  const result: unknown = await namespace.call(method, params);
  if (isDomainError(result)) throw new ActivityDomainError(result.error.code, result.error.message);
  return result as TResult;
}

function isDomainError(value: unknown): value is DomainErrorPayload {
  if (!value || typeof value !== "object") return false;
  const error = (value as { error?: unknown }).error;
  return Boolean(
    error &&
      typeof error === "object" &&
      typeof (error as { code?: unknown }).code === "string" &&
      typeof (error as { message?: unknown }).message === "string",
  );
}

function validateActivityHealth(value: unknown): ActivityHealth {
  const record = value && typeof value === "object" ? (value as Record<string, unknown>) : null;
  const detectedVersion =
    record && Number.isInteger(record.ProtocolVersion) ? (record.ProtocolVersion as number) : null;
  const modVersion = record && typeof record.ModVersion === "string" ? record.ModVersion : null;

  if (detectedVersion !== SUPPORTED_PROTOCOL_VERSION)
    throw new ActivityProtocolMismatchError(
      SUPPORTED_PROTOCOL_VERSION,
      detectedVersion,
      modVersion,
    );

  return value as ActivityHealth;
}
