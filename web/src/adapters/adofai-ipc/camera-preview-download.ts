import type { CameraPreviewDownloads } from "@/ports/camera-preview";

export function createCameraPreviewDownloads(): CameraPreviewDownloads {
  return {
    async readFrame({ file, width, height }, options) {
      const stride = (width * 3 + 3) & ~3;
      if (
        !Number.isInteger(width) ||
        width < 2 ||
        width > 640 ||
        !Number.isInteger(height) ||
        height < 2 ||
        height > 480 ||
        file.size !== 54 + stride * height ||
        file.size > 921654
      )
        throw new Error("Invalid camera preview dimensions");
      const bytes = new Uint8Array(file.size);
      const reader = file.stream(options).getReader();
      let offset = 0;
      let complete = false;
      try {
        while (true) {
          const chunk = await reader.read();
          if (chunk.done) break;
          if (offset + chunk.value.length > bytes.length)
            throw new Error("Oversized camera preview");
          bytes.set(chunk.value, offset);
          offset += chunk.value.length;
        }
        if (offset !== bytes.length) throw new Error("Incomplete camera preview");
        const header = new DataView(bytes.buffer);
        if (
          header.getUint16(0, true) !== 0x4d42 ||
          header.getUint32(2, true) !== bytes.length ||
          header.getUint32(10, true) !== 54 ||
          header.getUint32(14, true) !== 40 ||
          header.getInt32(18, true) !== width ||
          header.getInt32(22, true) !== height ||
          header.getUint16(26, true) !== 1 ||
          header.getUint16(28, true) !== 24 ||
          header.getUint32(30, true) !== 0
        )
          throw new Error("Invalid camera preview bitmap");
        complete = true;
        return { bytes, width, height };
      } finally {
        if (!complete) await reader.cancel().catch(() => {});
        reader.releaseLock();
      }
    },
  };
}
