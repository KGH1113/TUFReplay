import type { CameraPreviewDownloads } from "@/ports/camera-preview";

export function createCameraPreviewDownloads(
  fetchFrame: (input: string, init?: RequestInit) => Promise<Response> = globalThis.fetch,
): CameraPreviewDownloads {
  return {
    async readFrame(ticket, options) {
      const url = new URL(ticket.url);
      const { width, height } = ticket.metadata;
      const stride = (width * 3 + 3) & ~3;
      if (
        url.protocol !== "http:" ||
        url.hostname !== "127.0.0.1" ||
        Number(url.port) < 32145 ||
        Number(url.port) > 32155 ||
        url.username ||
        url.password ||
        url.search ||
        url.hash ||
        !/^\/ipc\/download\/[A-Za-z0-9_-]+$/.test(url.pathname) ||
        ticket.byteLength !== 54 + stride * height ||
        ticket.byteLength > 921654
      )
        throw new Error("Invalid camera preview ticket");
      const response = await fetchFrame(url.href, {
        signal: options?.signal,
        cache: "no-store",
        credentials: "omit",
        redirect: "error",
      });
      if (!response.ok || !response.body) throw new Error("Camera preview download failed");
      const bytes = new Uint8Array(ticket.byteLength);
      const reader = response.body.getReader();
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
