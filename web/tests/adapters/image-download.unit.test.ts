import { describe, expect, test } from "bun:test";
import { ReceivedFile } from "adofai-ipc";
import { createImageDownloads } from "@/adapters/adofai-ipc/image-download";

const signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
const downloads = createImageDownloads();

function file(bytes: Uint8Array, size = bytes.length) {
  return new ReceivedFile(
    "noFail.png",
    "image/png",
    size,
    () =>
      new ReadableStream({
        start(controller) {
          controller.enqueue(bytes.slice(0, 3));
          controller.enqueue(bytes.slice(3));
          controller.close();
        },
      }),
  );
}

describe("game icon image adapter", () => {
  test("reads a PNG split across chunks into an image blob", async () => {
    const bytes = new Uint8Array([...signature, 1, 2, 3, 4]);
    const blob = await downloads.readPng(file(bytes));
    expect(blob.type).toBe("image/png");
    expect(new Uint8Array(await blob.arrayBuffer())).toEqual(bytes);
  });

  test("rejects bytes that are not a PNG", async () => {
    await expect(downloads.readPng(file(new Uint8Array(12)))).rejects.toMatchObject({
      code: "invalid_image_download",
    });
  });

  test("rejects a file that is shorter or longer than announced", async () => {
    const bytes = new Uint8Array([...signature, 1, 2]);
    await expect(downloads.readPng(file(bytes, bytes.length + 1))).rejects.toMatchObject({
      code: "invalid_image_download",
    });
    await expect(downloads.readPng(file(bytes, bytes.length - 1))).rejects.toMatchObject({
      code: "invalid_image_download",
    });
  });
});
