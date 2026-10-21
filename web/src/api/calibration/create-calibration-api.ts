import type { CalibrationState } from "@tuf-replay/ipc";
import type { CalibrationApi } from "@/api/calibration/calibration-api";
import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toCalibrationResultDto, toCalibrationStateDto } from "@/api/recorder-dtos";
import { mapCalibrationResult, mapCalibrationStatus } from "@/models/calibration/calibration-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import {
  calibrationResultDtoSchema,
  calibrationStatusDtoSchema,
} from "@/schemas/calibration/calibration-schema";
export function createCalibrationApi(clients: LocalAppChannels): CalibrationApi {
  const status = (value: CalibrationState) =>
    mapCalibrationStatus(decodeRecorder(calibrationStatusDtoSchema, toCalibrationStateDto(value)));
  return {
    start: async () => status(await callRecorder(clients, (mod) => mod.startCalibration())),
    getStatus: async (operationId) =>
      status(await callRecorder(clients, (mod) => mod.readCalibrationState({ operationId }))),
    async getResult(operationId, revision) {
      return mapCalibrationResult(
        decodeRecorder(
          calibrationResultDtoSchema,
          toCalibrationResultDto(
            await callRecorder(clients, (mod) =>
              mod.readCalibrationResult({ operationId, revision }),
            ),
          ),
        ),
      );
    },
    playPreview: async (operationId) =>
      status(await callRecorder(clients, (mod) => mod.startCalibrationPreview({ operationId }))),
    stopPreview: async (operationId) =>
      status(await callRecorder(clients, (mod) => mod.stopCalibrationPreview({ operationId }))),
    setOffset: async (operationId, offsetMs) =>
      status(
        await callRecorder(clients, (mod) =>
          mod.changeCalibrationOffset({ operationId, offsetMs }),
        ),
      ),
    setVolume: async (operationId, volumeDb) =>
      status(
        await callRecorder(clients, (mod) =>
          mod.changeCalibrationVolume({ operationId, volumeDb }),
        ),
      ),
    close: async (operationId) =>
      status(await callRecorder(clients, (mod) => mod.closeCalibration({ operationId }))),
  };
}
