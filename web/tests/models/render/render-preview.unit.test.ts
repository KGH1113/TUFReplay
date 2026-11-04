import { describe, expect, test } from "bun:test";
import {
  openForPreview,
  previewSizeLimit,
  previewSkipReason,
} from "@/models/render/render-preview";
import type { TransferredFile } from "@/ports/received-file";

const playsMp4 = (type: string) => type === "video/mp4";

function videoFile(open?: TransferredFile["open"]): TransferredFile {
  return {
    name: "render.mp4",
    type: "video/mp4",
    size: 10 * 1024 ** 3,
    stream: () => new ReadableStream(),
    ...(open ? { open } : {}),
  };
}

describe("render preview", () => {
  test("previews a playable video of a reasonable size", () => {
    expect(previewSkipReason({ type: "video/mp4", size: 200 * 1024 ** 2 }, playsMp4)).toBeNull();
  });

  test("skips a format the browser cannot play before loading anything", () => {
    expect(previewSkipReason({ type: "video/quicktime", size: 1024 }, playsMp4)).toBe("format");
    expect(previewSkipReason({ type: "", size: 1024 }, playsMp4)).toBe("format");
    expect(previewSkipReason({ type: "video/quicktime", size: 1024 }, playsMp4, true)).toBe(
      "format",
    );
  });

  test("streams a video of any size but copies only one that fits the limit", () => {
    const large = { type: "video/mp4", size: previewSizeLimit + 1 };
    expect(previewSkipReason(large, playsMp4)).toBe("size");
    expect(previewSkipReason(large, playsMp4, true)).toBeNull();
    expect(previewSkipReason({ type: "video/mp4", size: previewSizeLimit }, playsMp4)).toBeNull();
  });

  test("streams from the game when it can open the file", async () => {
    const opened = { url: "http://127.0.0.1:32145/ipc/files/token", close: async () => {} };
    const file = videoFile(async () => opened);
    expect(await openForPreview(file, new AbortController().signal)).toBe(opened);
  });

  test("falls back to copying when the game cannot stream", async () => {
    expect(await openForPreview(videoFile(), new AbortController().signal)).toBeNull();
    const refused = videoFile(async () => {
      throw Object.assign(new Error("old runtime"), { code: "file_open_unavailable" });
    });
    expect(await openForPreview(refused, new AbortController().signal)).toBeNull();
  });

  test("stops when the preview is closed while opening", async () => {
    const abort = new AbortController();
    const file = videoFile(async () => {
      abort.abort();
      throw new Error("aborted");
    });
    await expect(openForPreview(file, abort.signal)).rejects.toThrow("aborted");
  });
});
