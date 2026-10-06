import { describe, expect, test } from "bun:test";
import { createCameraPreviewDownloads } from "@/adapters/adofai-ipc/camera-preview-download";
import { createWebcamApi } from "@/api/webcam/create-webcam-api";
import { createWebcamApiMock } from "@/mocks/webcam/create-webcam-api-mock";
import { scriptedChannels } from "../../fixtures/local-message-peer";

describe("shared camera preview contract", () => {
  test("uses the game camera ID and transfers bitmap bytes outside the control message", async () => {
    const mock = createWebcamApiMock();
    await mock.updateSettings({ enabled: true });
    const frame = await mock.getPreviewFrame(null);
    if (!frame) throw new Error("Missing fixture");
    const ticket = {
      url: "http://127.0.0.1:32145/ipc/download/camera",
      byteLength: frame.bytes.length,
      metadata: { width: frame.width, height: frame.height },
    };
    const calls: unknown[] = [];
    const api = createWebcamApi(
      scriptedChannels((command, payload) => {
        calls.push({ command, payload });
        return ticket;
      }),
      createCameraPreviewDownloads(async () => new Response(frame.bytes)),
    );
    const result = await api.getPreviewFrame("native-camera");
    expect(result?.bytes).toEqual(frame.bytes);
    expect(calls).toEqual([
      { command: "webcam.preview.read", payload: { deviceId: "native-camera" } },
    ]);
  });

  test("warmup does not create an HTTP download", async () => {
    const api = createWebcamApi(
      scriptedChannels(() => ({ ready: false })),
      {
        async readFrame() {
          throw new Error("Unexpected download");
        },
      },
    );
    expect(await api.getPreviewFrame(null)).toBe(null);
  });

  test("rejects an arbitrary download target before fetching", async () => {
    let fetched = false;
    const adapter = createCameraPreviewDownloads(async () => {
      fetched = true;
      return new Response();
    });
    for (const url of [
      "https://example.com/camera",
      "http://127.0.0.1:32145/settings",
      "http://127.0.0.1:32145/ipc/download/camera?token=bad",
    ]) {
      await expect(
        adapter.readFrame({ url, byteLength: 70, metadata: { width: 2, height: 2 } }),
      ).rejects.toThrow();
    }
    expect(fetched).toBe(false);
  });

  test("rejects missing, oversized, or malformed frame bytes", async () => {
    const ticket = {
      url: "http://127.0.0.1:32145/ipc/download/camera",
      byteLength: 70,
      metadata: { width: 2, height: 2 },
    };
    for (const length of [69, 71, 70]) {
      const adapter = createCameraPreviewDownloads(
        async () => new Response(new Uint8Array(length)),
      );
      await expect(adapter.readFrame(ticket)).rejects.toThrow();
    }
  });
});
