import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toWebcamStateDto } from "@/api/recorder-dtos";
import type { WebcamApi } from "@/api/webcam/webcam-api";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import type { CameraPreviewDownloads } from "@/ports/camera-preview";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";
export function createWebcamApi(
  clients: LocalAppChannels,
  previews: CameraPreviewDownloads,
): WebcamApi {
  return {
    async getPreviewFrame(deviceId, options) {
      const frame = await callRecorder(
        clients,
        (mod) => mod.readWebcamPreview({ deviceId }),
        options,
      );
      return frame === null ? null : previews.readFrame(frame, options);
    },
    async getSettings(refreshDevices = false) {
      return mapWebcamState(
        decodeRecorder(
          webcamStateDtoSchema,
          toWebcamStateDto(
            await callRecorder(clients, (mod) =>
              mod.refreshWebcamState(refreshDevices ? { refreshDevices: true } : {}),
            ),
          ),
        ),
      );
    },
    async updateSettings(patch) {
      const settings = webcamSettingsPatchSchema.parse(patch);
      return mapWebcamState(
        decodeRecorder(
          webcamStateDtoSchema,
          toWebcamStateDto(
            await callRecorder(clients, (mod) => mod.changeWebcamSettings({ settings })),
          ),
        ),
      );
    },
  };
}
