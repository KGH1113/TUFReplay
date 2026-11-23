import type { ImageDownloads } from "@/ports/image-download";
import { ApiError } from "@/shared/errors/api-error";

const MAX_PNG_BYTES = 4 * 1024 * 1024;
const PNG_SIGNATURE = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

export function createImageDownloads(): ImageDownloads {
  return {
    async readPng(file, options) {
      if (
        !Number.isSafeInteger(file.size) ||
        file.size < PNG_SIGNATURE.length ||
        file.size > MAX_PNG_BYTES
      )
        throw invalidImage();
      const bytes = new Uint8Array(file.size);
      const reader = file.stream(options).getReader();
      let offset = 0;
      let complete = false;
      try {
        while (true) {
          const chunk = await reader.read();
          if (chunk.done) break;
          if (offset + chunk.value.byteLength > bytes.length) throw invalidImage();
          bytes.set(chunk.value, offset);
          offset += chunk.value.byteLength;
        }
        if (offset !== bytes.length || PNG_SIGNATURE.some((byte, index) => bytes[index] !== byte))
          throw invalidImage();
        complete = true;
        return new Blob([bytes], { type: "image/png" });
      } finally {
        if (!complete) await reader.cancel().catch(() => {});
        reader.releaseLock();
      }
    },
  };
}

function invalidImage() {
  return new ApiError("The game sent an incomplete or invalid image.", {
    kind: "validation",
    code: "invalid_image_download",
  });
}
