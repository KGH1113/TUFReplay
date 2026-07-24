import { AdofaiIpcClient, type AdofaiIpcNamespaceClient, tryConnect } from "@adofai-ipc/client";

import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLevelSessionOverview,
  ActivityLogicalLevelOverview,
  ActivityRun,
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
const FILE_PICKER_TIMEOUT_MS = 24 * 60 * 60 * 1000;

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

export interface ActivityGateway {
  health(): Promise<unknown>;
  listAllAppSessions(onPage?: (items: ActivityAppSession[]) => void): Promise<ActivityAppSession[]>;
  getLevelSession(id: string): Promise<ActivityLevelSessionOverview>;
  getLogicalLevel(id: string): Promise<ActivityLogicalLevelOverview>;
  listAllRuns(id: string, onPage?: (items: ActivityRun[]) => void): Promise<ActivityRun[]>;
  listAllLogicalLevelRuns(
    id: string,
    onPage?: (items: ActivityRun[]) => void,
  ): Promise<ActivityRun[]>;
  getChart(id: string): Promise<ActivityChart>;
  getLogicalLevelChart(id: string): Promise<ActivityChart>;
  deleteMicrophoneRecording(runId: string): Promise<MicrophoneRecordingDeleteResult>;
  keepMicrophoneRecording(runId: string): Promise<MicrophoneRecordingKeepResult>;
  playReplay(runId: string, levelPath?: string): Promise<ReplayStatus>;
  getReplayStatus(): Promise<ReplayStatus>;
  pickReplayLevelFile(runId: string): Promise<ReplayLevelFilePickerResult>;
  getMicrophoneDevices(): Promise<MicrophoneDevicesState>;
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
  const pickerClient = new AdofaiIpcClient({
    baseUrl: client.baseUrl,
    fetch: adofaiIpcFetch,
    timeoutMs: FILE_PICKER_TIMEOUT_MS,
  });
  return createActivityGateway(client.namespace(NAMESPACE), pickerClient.namespace(NAMESPACE));
}

export function createActivityGateway(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
  pickerNamespace: Pick<AdofaiIpcNamespaceClient, "call"> = namespace,
): ActivityGateway {
  return {
    health: () => callDomain(namespace, "health.get", {}),
    listAllAppSessions: (onPage) =>
      loadAllPages<ActivityAppSession>(
        (offset, limit) =>
          callDomain<ActivityAppSession[]>(namespace, "activity.app-sessions.list", {
            offset,
            limit,
          }),
        onPage,
      ),
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
    listAllLogicalLevelRuns: (id, onPage) =>
      loadAllPages<ActivityRun>(
        (offset, limit) =>
          callDomain<ActivityRun[]>(namespace, "activity.logical-level.runs.list", {
            id,
            offset,
            limit,
          }),
        onPage,
      ),
    getLogicalLevelChart: (id) => callDomain(namespace, "activity.logical-level.chart.get", { id }),
    deleteMicrophoneRecording: (runId) =>
      callDomain(namespace, "microphone.recording.delete", { runId }),
    keepMicrophoneRecording: (runId) =>
      callDomain(namespace, "microphone.recording.keep", { runId }),
    playReplay: (runId, levelPath) =>
      callDomain(namespace, "replay.play", levelPath ? { runId, levelPath } : { runId }),
    getReplayStatus: () => callDomain(namespace, "replay.status.get", {}),
    pickReplayLevelFile: (runId) =>
      callDomain(pickerNamespace, "replay.level-file.pick", { runId }),
    getMicrophoneDevices: () => callDomain(namespace, "microphone.devices.get", {}),
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
