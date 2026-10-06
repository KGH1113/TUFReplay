import type { WebcamSettingsPatch, WebcamState } from "@/models/webcam/webcam-model";

import type { CameraPreviewFrame } from "@/ports/camera-preview";

export interface WebcamApi {
  getPreviewFrame(
    deviceId: string | null,
    options?: { signal?: AbortSignal },
  ): Promise<CameraPreviewFrame | null>;
  getSettings(refreshDevices?: boolean): Promise<WebcamState>;
  updateSettings(patch: WebcamSettingsPatch): Promise<WebcamState>;
}
