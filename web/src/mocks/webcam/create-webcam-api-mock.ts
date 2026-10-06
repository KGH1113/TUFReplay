import type { WebcamApi } from "@/api/webcam/webcam-api";
import type { WebcamState } from "@/models/webcam/webcam-model";
import { mapWebcamState } from "@/models/webcam/webcam-model";
import { webcamSettingsPatchSchema, webcamStateDtoSchema } from "@/schemas/webcam/webcam-schema";

export function createWebcamApiMock(onState?: (state: WebcamState) => void): WebcamApi {
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
    async getPreviewFrame() {
      if (!state.enabled) return null;
      const width = 640,
        height = 360,
        stride = width * 3;
      const bytes = new Uint8Array(54 + stride * height);
      const header = new DataView(bytes.buffer);
      header.setUint16(0, 0x4d42, true);
      header.setUint32(2, bytes.length, true);
      header.setUint32(10, 54, true);
      header.setUint32(14, 40, true);
      header.setInt32(18, width, true);
      header.setInt32(22, height, true);
      header.setUint16(26, 1, true);
      header.setUint16(28, 24, true);
      header.setUint32(34, stride * height, true);
      for (let y = 0; y < height; y++)
        for (let x = 0; x < width; x++) {
          const offset = 54 + y * stride + x * 3;
          bytes[offset] = x < width / 2 ? 160 : 50;
          bytes[offset + 1] = y < height / 2 ? 180 : 70;
          bytes[offset + 2] = x > width / 2 ? 210 : 40;
        }
      return { bytes, width, height };
    },
    async getSettings() {
      return structuredClone(state);
    },
    async updateSettings(patch) {
      state = { ...state, ...webcamSettingsPatchSchema.parse(patch) };
      state.status = state.enabled ? "ready" : "off";
      onState?.(structuredClone(state));
      return structuredClone(state);
    },
  };
}
