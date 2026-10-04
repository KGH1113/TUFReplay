import type { WebcamSettingsPatch, WebcamState } from "@/models/webcam/webcam-model";

export interface WebcamApi {
  getSettings(refreshDevices?: boolean): Promise<WebcamState>;
  updateSettings(patch: WebcamSettingsPatch): Promise<WebcamState>;
}
