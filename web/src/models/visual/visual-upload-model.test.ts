import { expect, test } from "bun:test";
import { readVisualUploads } from "./visual-upload-model";

test("font upload preserves the requested reference and binary bytes across chunks", async () => {
  const bytes = Uint8Array.from({ length: 20_000 }, (_, index) => index % 256);
  const [upload] = await readVisualUploads({ "Font/한글.otf": new File([bytes], "font.otf") });
  expect(upload?.reference).toBe("Font/한글.otf");
  if (!upload) throw new Error("Expected an uploaded font");
  expect(Uint8Array.from(atob(upload.data_base64), (c) => c.charCodeAt(0))).toEqual(bytes);
});

test("empty and oversized assets are rejected before reading their contents", async () => {
  await expect(readVisualUploads({ font: new File([], "empty.otf") })).rejects.toThrow(
    "visual_payload_too_large",
  );
  let read = false;
  const oversized = {
    size: 48 * 1024 * 1024 + 1,
    arrayBuffer: async () => {
      read = true;
      return new ArrayBuffer(0);
    },
  } as File;
  await expect(readVisualUploads({ font: oversized })).rejects.toThrow("visual_payload_too_large");
  expect(read).toBe(false);
});
