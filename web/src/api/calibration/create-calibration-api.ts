import type { CalibrationApi } from "@/api/calibration/calibration-api";
import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import { mapCalibrationResult, mapCalibrationStatus } from "@/models/calibration/calibration-model";
import {
  calibrationResultDtoSchema,
  calibrationStatusDtoSchema,
} from "@/schemas/calibration/calibration-schema";

export function createCalibrationApi(clients: AdofaiIpcClients): CalibrationApi {
  const statusCall = async (method: string, params: object) =>
    mapCalibrationStatus(
      await sendDomainCommand(
        clients.namespace,
        method,
        "calibration.state.changed",
        params,
        calibrationStatusDtoSchema,
      ),
    );
  return {
    start: () => statusCall("calibration.start", {}),
    getStatus: (operationId) => statusCall("calibration.state.read", { operationId }),
    async getResult(operationId, revision) {
      return mapCalibrationResult(
        await sendDomainCommand(
          clients.namespace,
          "calibration.result.read",
          "calibration.result.ready",
          { operationId, revision },
          calibrationResultDtoSchema,
        ),
      );
    },
    playPreview: (operationId) => statusCall("calibration.preview.start", { operationId }),
    stopPreview: (operationId) => statusCall("calibration.preview.stop", { operationId }),
    setOffset: (operationId, offsetMs) =>
      statusCall("calibration.offset.change", { operationId, offsetMs }),
    setVolume: (operationId, volumeDb) =>
      statusCall("calibration.volume.change", { operationId, volumeDb }),
    close: (operationId) => statusCall("calibration.close", { operationId }),
  };
}
