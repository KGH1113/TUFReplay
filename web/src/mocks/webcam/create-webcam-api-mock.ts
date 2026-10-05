import type { WebcamApi } from "@/api/webcam/webcam-api";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";

export function createWebcamApiMock(): WebcamApi {
  let state = mapWebcamState(
    webcamStateDtoSchema.parse({
      Supported: true,
      Backend: "avfoundation",
      Enabled: false,
      CaptureLocked: false,
      Status: "off",
      Error: null,
      Devices: [{ Id: "mock-camera", Name: "FaceTime HD Camera" }],
      SelectedDeviceId: null,
      Quality: "compact",
      OffsetMs: 0,
      StorageLimitMb: 512,
      RetentionDays: 7,
      PlaybackVisible: true,
      LiveVisible: false,
      Mirror: false,
      Crop: { X: 0, Y: 0, Width: 1, Height: 1 },
    }),
  );
  return {
    async getSettings() {
      return structuredClone(state);
    },
    async updateSettings(patch) {
      state = { ...state, ...webcamSettingsPatchSchema.parse(patch) };
      state.status = state.enabled ? "ready" : "off";
      return structuredClone(state);
    },
  };
}
