import { describe, expect, test } from "bun:test";
import { createWebcamApi } from "@/api/webcam/create-webcam-api";
import { createWebcamApiMock } from "@/mocks/webcam/create-webcam-api-mock";
import { scriptedChannels as clientsWith } from "../../fixtures/local-message-peer";

function stateDto() {
  return {
    Supported: true,
    Backend: "avfoundation",
    Enabled: false,
    CaptureLocked: false,
    Status: "off",
    Error: null,
    Devices: [{ Id: "camera-1", Name: "USB camera" }],
    SelectedDeviceId: null,
    FfmpegPath: null,
    Quality: "compact",
    OffsetMs: 0,
    StorageLimitMb: 512,
    RetentionDays: 7,
    PlaybackVisible: true,
    Mirror: false,
    Crop: { X: 0, Y: 0, Width: 1, Height: 1 },
  };
}

describe("webcam IPC contract", () => {
  test("maps crop state and sends display settings without enabling the camera", async () => {
    const calls: { method: string; params: unknown }[] = [];
    const api = createWebcamApi(
      clientsWith((method, params) => {
        calls.push({ method, params });
        return stateDto();
      }),
    );
    const state = await api.getSettings();
    expect(state.enabled).toBe(false);
    expect(state.devices).toEqual([{ id: "camera-1", name: "USB camera" }]);
    expect(state.crop).toEqual({ x: 0, y: 0, width: 1, height: 1 });
    await api.updateSettings({ crop: { x: 0.25, y: 0, width: 0.5, height: 0.6 }, offsetMs: 120 });
    expect(calls).toEqual([
      { method: "webcam.state.refresh", params: {} },
      {
        method: "webcam.settings.change",
        params: { crop: { x: 0.25, y: 0, width: 0.5, height: 0.6 }, offsetMs: 120 },
      },
    ]);
    await api.getSettings(true);
    expect(calls.at(-1)).toEqual({
      method: "webcam.state.refresh",
      params: { refreshDevices: true },
    });
  });

  test("rejects unsafe settings before sending IPC", async () => {
    let calls = 0;
    const api = createWebcamApi(
      clientsWith(() => {
        calls++;
        return stateDto();
      }),
    );
    for (const patch of [
      { overlayX: 0.5 },
      { overlayWidth: 0.3 },
      { crop: { x: 0.8, y: 0, width: 0.3, height: 1 } },
      { crop: { x: 0, y: 0, width: 0.04, height: 1 } },
      { crop: { x: 0, y: 0.9, width: 1, height: 0.2 } },
      { crop: { x: 0, y: 0, width: Number.NaN, height: 1 } },
      { offsetMs: 1001 },
      { offsetMs: 0.5 },
      { storageLimitMb: 1 },
      { retentionDays: 0 },
      { enabled: "true" },
      { unknown: true },
    ]) {
      await expect(api.updateSettings(patch as never)).rejects.toThrow();
    }
    expect(calls).toBe(0);
  });

  test("rejects malformed state and preserves capture-lock domain errors", async () => {
    const malformed = createWebcamApi(
      clientsWith(() => ({ ...stateDto(), Crop: { X: 0, Y: 0, Width: Number.NaN, Height: 1 } })),
    );
    await expect(malformed.getSettings()).rejects.toMatchObject({
      kind: "validation",
      code: "invalid_response",
    });
    const locked = createWebcamApi(
      clientsWith(() => ({
        error: { code: "webcam_settings_locked", message: "Capture is active" },
      })),
    );
    await expect(locked.updateSettings({ enabled: false })).rejects.toMatchObject({
      kind: "domain",
      code: "webcam_settings_locked",
    });
  });

  test("keeps mock camera opt-in and returned snapshots independent", async () => {
    const api = createWebcamApiMock();
    const initial = await api.getSettings();
    initial.devices[0].name = "changed outside the API";
    const cropped = await api.updateSettings({
      crop: { x: 0.1, y: 0.2, width: 0.6, height: 0.5 },
      mirror: true,
    });
    expect(cropped.enabled).toBe(false);
    expect(cropped.devices[0].name).toBe("FaceTime HD Camera");
    expect(cropped.crop.x).toBe(0.1);
    expect((await api.updateSettings({ enabled: true, quality: "balanced" })).status).toBe("ready");
    expect((await api.getSettings()).mirror).toBe(true);
  });

  test("persists Quality, live preview, and crop without starting recording", async () => {
    const api = createWebcamApiMock();
    const state = await api.updateSettings({
      quality: "quality",
      liveVisible: true,
      crop: { x: 0.1, y: 0.2, width: 0.7, height: 0.6 },
    });
    expect(state.quality).toBe("quality");
    expect(state.liveVisible).toBe(true);
    expect(state.enabled).toBe(false);
    expect((await api.getSettings()).crop).toEqual({ x: 0.1, y: 0.2, width: 0.7, height: 0.6 });
  });
});
