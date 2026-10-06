import type { TextDownloads } from "@/ports/text-download";
import { ApiError } from "@/shared/errors/api-error";

export function createLocalTextDownloads(
  fetchFile: typeof fetch = globalThis.fetch,
): TextDownloads {
  return {
    async readText(ticket, options) {
      const url = parseUrl(ticket.url);
      if (
        url.protocol !== "http:" ||
        url.hostname !== "127.0.0.1" ||
        Number(url.port) < 32145 ||
        Number(url.port) > 32155 ||
        url.username !== "" ||
        url.password !== "" ||
        url.search !== "" ||
        url.hash !== "" ||
        !/^\/ipc\/download\/[A-Za-z0-9_-]+$/.test(url.pathname) ||
        !Number.isSafeInteger(ticket.byteLength) ||
        ticket.byteLength < 0
      )
        throw invalidDownload();

      const controller = new AbortController();
      const abort = () => controller.abort(options?.signal?.reason);
      options?.signal?.addEventListener("abort", abort, { once: true });
      if (options?.signal?.aborted) abort();
      const timeout = setTimeout(() => controller.abort(), 30_000);
      try {
        const response = await fetchFile(url.href, {
          signal: controller.signal,
          cache: "no-store",
          credentials: "omit",
          redirect: "error",
        });
        if (!response.ok)
          throw new ApiError("The chart download could not be completed. Try loading it again.", {
            kind: "http",
            code: "chart_download_failed",
          });
        if (!response.body) throw invalidDownload();

        const reader = response.body.getReader();
        const decoder = new TextDecoder("utf-8", { fatal: true });
        const decode = (bytes?: Uint8Array, stream = false) => {
          try {
            return decoder.decode(bytes, { stream });
          } catch (cause) {
            throw invalidDownload(cause);
          }
        };
        const text: string[] = [];
        let received = 0;
        let complete = false;
        try {
          while (true) {
            const chunk = await reader.read();
            if (chunk.done) break;
            received += chunk.value.byteLength;
            if (received > ticket.byteLength) throw invalidDownload();
            text.push(decode(chunk.value, true));
          }
          if (received !== ticket.byteLength) throw invalidDownload();
          text.push(decode());
          complete = true;
          return text.join("");
        } finally {
          if (!complete) await reader.cancel().catch(() => {});
          reader.releaseLock();
        }
      } catch (cause) {
        if (cause instanceof ApiError || options?.signal?.aborted) throw cause;
        throw new ApiError("The chart download was interrupted. Try loading it again.", {
          kind: "connection",
          code: "chart_download_failed",
          cause,
        });
      } finally {
        clearTimeout(timeout);
        options?.signal?.removeEventListener("abort", abort);
      }
    },
  };
}

function parseUrl(value: string) {
  try {
    return new URL(value);
  } catch (cause) {
    throw invalidDownload(cause);
  }
}

function invalidDownload(cause?: unknown) {
  return new ApiError("The chart download is incomplete or invalid. Try loading it again.", {
    kind: "validation",
    code: "invalid_chart_download",
    cause,
  });
}
