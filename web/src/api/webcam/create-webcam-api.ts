import { z } from "zod";
import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { WebcamApi } from "@/api/webcam/webcam-api";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import type { CameraPreviewDownloads } from "@/ports/camera-preview";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";

export function createWebcamApi(
  clients: AdofaiIpcClients,
  previews: CameraPreviewDownloads,
): WebcamApi {
  return {
    async getPreviewFrame(deviceId, options) {
      const frame = await sendDomainCommand(
        clients.namespace,
        "webcam.preview.read",
        "webcam.preview.frame",
        { deviceId },
        z.union([
          z.object({ ready: z.literal(false) }).strict(),
          z.object({
            url: z.string(),
            byteLength: z.number().int().positive().max(921654),
            metadata: z.object({
              width: z.number().int().min(2).max(640),
              height: z.number().int().min(2).max(480),
            }),
          }),
        ]),
        options,
      );
      return "ready" in frame ? null : previews.readFrame(frame, options);
    },
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
