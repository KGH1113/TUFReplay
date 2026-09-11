import type { CalibrationApi } from "@/api/calibration/calibration-api";
import { mapCalibrationResult, mapCalibrationStatus } from "@/models/calibration/calibration-model";
import {
  calibrationResultDtoSchema,
  calibrationStatusDtoSchema,
} from "@/schemas/calibration/calibration-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

export function createCalibrationApi(clients: AdofaiIpcClients): CalibrationApi {
  const statusCall = async (method: string, params: object) =>
    mapCalibrationStatus(
      await callAdofaiIpc(clients.namespace, method, params, calibrationStatusDtoSchema),
    );
  return {
    start: () => statusCall("microphone.calibration.start", {}),
    getStatus: (operationId) => statusCall("microphone.calibration.status.get", { operationId }),
    async getResult(operationId, revision) {
      return mapCalibrationResult(
        await callAdofaiIpc(
          clients.namespace,
          "microphone.calibration.result.get",
          { operationId, revision },
          calibrationResultDtoSchema,
        ),
      );
    },
    playPreview: (operationId) =>
      statusCall("microphone.calibration.preview.play", { operationId }),
    stopPreview: (operationId) =>
      statusCall("microphone.calibration.preview.stop", { operationId }),
    setOffset: (operationId, offsetMs) =>
      statusCall("microphone.calibration.offset.set", { operationId, offsetMs }),
    setVolume: (operationId, volumeDb) =>
      statusCall("microphone.calibration.volume.set", { operationId, volumeDb }),
    close: (operationId) => statusCall("microphone.calibration.close", { operationId }),
  };
}
