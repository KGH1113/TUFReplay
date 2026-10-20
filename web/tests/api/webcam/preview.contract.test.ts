import { describe, expect, test } from "bun:test";
import { ReceivedFile } from "adofai-ipc";
import { createCameraPreviewDownloads } from "@/adapters/adofai-ipc/camera-preview-download";
import { createWebcamApi } from "@/api/webcam/create-webcam-api";
import { createWebcamApiMock } from "@/mocks/webcam/create-webcam-api-mock";
import { scriptedChannels } from "../../fixtures/local-message-peer";

function file(bytes: Uint8Array, size = bytes.length) {
  return new ReceivedFile(
    "preview.bmp",
    "image/bmp",
    size,
    () =>
      new ReadableStream({
        start(controller) {
          controller.enqueue(bytes);
          controller.close();
        },
      }),
  );
}
describe("shared camera preview contract", () => {
  test("uses the game camera ID and consumes a typed file once", async () => {
    const mock = createWebcamApiMock();
    await mock.updateSettings({ enabled: true });
    const frame = await mock.getPreviewFrame(null);
    if (!frame) throw new Error("Missing fixture");
    const preview = { file: file(frame.bytes), width: frame.width, height: frame.height };
    const calls: unknown[] = [];
    const api = createWebcamApi(
      scriptedChannels((command, payload) => {
        calls.push({ command, payload });
        return preview;
      }),
      createCameraPreviewDownloads(),
    );
    expect((await api.getPreviewFrame("native-camera"))?.bytes).toEqual(frame.bytes);
    expect(preview.file.used).toBe(true);
    expect(calls).toEqual([
      { command: "readWebcamPreview", payload: { deviceId: "native-camera" } },
    ]);
  });
  test("warmup does not consume a file", async () => {
    const api = createWebcamApi(
      scriptedChannels(() => null),
      {
        async readFrame() {
          throw new Error("Unexpected download");
        },
      },
    );
    expect(await api.getPreviewFrame(null)).toBe(null);
  });
  test("rejects oversized frame metadata before reading", async () => {
    const preview = file(new Uint8Array(70));
    await expect(
      createCameraPreviewDownloads().readFrame({ file: preview, width: 641, height: 2 }),
    ).rejects.toThrow();
    expect(preview.used).toBe(false);
  });
  test("rejects missing, oversized, or malformed frame bytes", async () => {
    for (const length of [69, 71, 70])
      await expect(
        createCameraPreviewDownloads().readFrame({
          file: file(new Uint8Array(length), 70),
          width: 2,
          height: 2,
        }),
      ).rejects.toThrow();
  });
});
