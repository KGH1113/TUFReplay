import type { WebcamApi } from "@/api/webcam/webcam-api";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

export function createWebcamApi(clients: AdofaiIpcClients): WebcamApi {
  return {
    async getSettings(refreshDevices = false) {
      return mapWebcamState(
        await callAdofaiIpc(
          clients.namespace,
          "webcam.settings.get",
          refreshDevices ? { refreshDevices: true } : {},
          webcamStateDtoSchema,
        ),
      );
    },
    async updateSettings(patch) {
      const params = webcamSettingsPatchSchema.parse(patch);
      return mapWebcamState(
        await callAdofaiIpc(
          clients.namespace,
          "webcam.settings.update",
          params,
          webcamStateDtoSchema,
        ),
      );
    },
  };
}
