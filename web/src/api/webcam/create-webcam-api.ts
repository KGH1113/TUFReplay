import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { WebcamApi } from "@/api/webcam/webcam-api";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";

export function createWebcamApi(clients: AdofaiIpcClients): WebcamApi {
  return {
    async getSettings(refreshDevices = false) {
      return mapWebcamState(
        await sendDomainCommand(
          clients.namespace,
          "webcam.state.refresh",
          "webcam.state.changed",
          refreshDevices ? { refreshDevices: true } : {},
          webcamStateDtoSchema,
        ),
      );
    },
    async updateSettings(patch) {
      const params = webcamSettingsPatchSchema.parse(patch);
      return mapWebcamState(
        await sendDomainCommand(
          clients.namespace,
          "webcam.settings.change",
          "webcam.state.changed",
          params,
          webcamStateDtoSchema,
        ),
      );
    },
  };
}
