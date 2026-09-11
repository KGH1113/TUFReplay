import { useRef } from "react";

import { useApiPromise } from "@/api/app-api-provider";
import type { CalibrationStatus } from "@/models/calibration/calibration-model";

export interface LegacyMicrophoneTimingSettings {
  MicrophoneOffsetMs: number;
  MicrophoneVolumeDb: number;
}

export interface LegacyCalibrationStatus extends LegacyMicrophoneTimingSettings {
  OperationId: string | null;
  State:
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
  ErrorCode: string | null;
  Message: string | null;
  DurationMs: number;
  PlaybackPositionMs: number;
  ResultRevision: number;
}

export interface LegacyCalibrationResult {
  OperationId: string;
  Revision: number;
  DurationMs: number;
  GameWaveform: number[];
  SongWaveform?: number[];
  MicrophoneWaveform: number[];
}

export interface CalibrationGateway {
  setMicrophoneOffset(offsetMs: number): Promise<LegacyMicrophoneTimingSettings>;
  setMicrophoneVolume(volumeDb: number): Promise<LegacyMicrophoneTimingSettings>;
  startMicrophoneCalibration(): Promise<LegacyCalibrationStatus>;
  getMicrophoneCalibrationStatus(operationId: string): Promise<LegacyCalibrationStatus>;
  getMicrophoneCalibrationResult(
    operationId: string,
    revision: number,
  ): Promise<LegacyCalibrationResult>;
  playMicrophoneCalibrationPreview(operationId: string): Promise<LegacyCalibrationStatus>;
  stopMicrophoneCalibrationPreview(operationId: string): Promise<LegacyCalibrationStatus>;
  setMicrophoneCalibrationOffset(
    operationId: string,
    offsetMs: number,
  ): Promise<LegacyCalibrationStatus>;
  setMicrophoneCalibrationVolume(
    operationId: string,
    volumeDb: number,
  ): Promise<LegacyCalibrationStatus>;
  closeMicrophoneCalibration(operationId: string): Promise<LegacyCalibrationStatus>;
}

export function useCalibrationGatewayAdapter() {
  const apiPromise = useApiPromise();
  const ref = useRef<CalibrationGateway | null>(null);
  ref.current = {
    setMicrophoneOffset: async (offsetMs) => {
      const next = await (await apiPromise).microphone.setOffset(offsetMs);
      return {
        MicrophoneOffsetMs: next.microphoneOffsetMs,
        MicrophoneVolumeDb: next.microphoneVolumeDb,
      };
    },
    setMicrophoneVolume: async (volumeDb) => {
      const next = await (await apiPromise).microphone.setVolume(volumeDb);
      return {
        MicrophoneOffsetMs: next.microphoneOffsetMs,
        MicrophoneVolumeDb: next.microphoneVolumeDb,
      };
    },
    startMicrophoneCalibration: async () =>
      toLegacyStatus(await (await apiPromise).calibration.start()),
    getMicrophoneCalibrationStatus: async (operationId) =>
      toLegacyStatus(await (await apiPromise).calibration.getStatus(operationId)),
    getMicrophoneCalibrationResult: async (operationId, revision) => {
      const next = await (await apiPromise).calibration.getResult(operationId, revision);
      return {
        OperationId: next.operationId,
        Revision: next.revision,
        DurationMs: next.durationMs,
        GameWaveform: next.gameWaveform,
        SongWaveform: next.songWaveform,
        MicrophoneWaveform: next.microphoneWaveform,
      };
    },
    playMicrophoneCalibrationPreview: async (operationId) =>
      toLegacyStatus(await (await apiPromise).calibration.playPreview(operationId)),
    stopMicrophoneCalibrationPreview: async (operationId) =>
      toLegacyStatus(await (await apiPromise).calibration.stopPreview(operationId)),
    setMicrophoneCalibrationOffset: async (operationId, offsetMs) =>
      toLegacyStatus(await (await apiPromise).calibration.setOffset(operationId, offsetMs)),
    setMicrophoneCalibrationVolume: async (operationId, volumeDb) =>
      toLegacyStatus(await (await apiPromise).calibration.setVolume(operationId, volumeDb)),
    closeMicrophoneCalibration: async (operationId) =>
      toLegacyStatus(await (await apiPromise).calibration.close(operationId)),
  };
  return ref;
}

function toLegacyStatus(next: CalibrationStatus) {
  return {
    OperationId: next.operationId,
    State: next.state,
    ErrorCode: next.errorCode,
    Message: next.message,
    DurationMs: next.durationMs,
    PlaybackPositionMs: next.playbackPositionMs,
    ResultRevision: next.resultRevision,
    MicrophoneOffsetMs: next.microphoneOffsetMs,
    MicrophoneVolumeDb: next.microphoneVolumeDb,
  };
}
