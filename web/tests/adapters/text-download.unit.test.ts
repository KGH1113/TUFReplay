import { describe, expect, test } from "bun:test";
import { ReceivedFile } from "adofai-ipc";
import { createLocalTextDownloads } from "@/adapters/adofai-ipc/text-download";

const encode = (text: string) => new TextEncoder().encode(text);
const downloads = createLocalTextDownloads();
function file(bytes: Uint8Array, size = bytes.length) {
  return new ReceivedFile(
    "chart.adofai",
    "application/json",
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
describe("typed chart file adapter", () => {
  test("decodes a large UTF-8 chart across arbitrary chunk boundaries", async () => {
    const expected = `{"comment":"${'한🙂"'.repeat(500_000)}"}`;
    const bytes = encode(expected);
    let offset = 0;
    const chart = new ReceivedFile(
      "chart.adofai",
      "application/json",
      bytes.length,
      () =>
        new ReadableStream({
          pull(controller) {
            if (offset === bytes.length) return controller.close();
            const end = Math.min(bytes.length, offset + 8191);
            controller.enqueue(bytes.slice(offset, end));
            offset = end;
          },
        }),
    );
    expect(bytes.length).toBeGreaterThan(2 * 1024 * 1024);
    expect(await downloads.readText(chart)).toBe(expected);
    expect(chart.used).toBe(true);
    await expect(downloads.readText(chart)).rejects.toThrow();
  });
  test("rejects invalid sizes before consuming the file", async () => {
    for (const size of [-1, Number.NaN, Number.MAX_SAFE_INTEGER + 1]) {
      const chart = file(encode("{}"), size);
      await expect(downloads.readText(chart)).rejects.toMatchObject({
        code: "invalid_chart_download",
      });
      expect(chart.used).toBe(false);
    }
  });
  test("rejects truncated or oversized content and cancels excess bytes", async () => {
    let cancelled = false;
    const excessive = new ReceivedFile(
      "chart.adofai",
      "application/json",
      2,
      () =>
        new ReadableStream({
          start(controller) {
            controller.enqueue(encode("too much data"));
          },
          cancel() {
            cancelled = true;
          },
        }),
    );
    await expect(downloads.readText(excessive)).rejects.toMatchObject({ kind: "validation" });
    expect(cancelled).toBe(true);
    await expect(downloads.readText(file(encode("{}"), 10))).rejects.toMatchObject({
      kind: "validation",
    });
  });
  test("propagates cancellation to the SDK file stream", async () => {
    const cancellation = new AbortController();
    const chart = new ReceivedFile("chart.adofai", "application/json", 2, (options) => {
      cancellation.abort(new DOMException("Download cancelled", "AbortError"));
      expect(options.signal?.aborted).toBe(true);
      throw options.signal?.reason;
    });
    await expect(downloads.readText(chart, { signal: cancellation.signal })).rejects.toMatchObject({
      name: "AbortError",
    });
  });
  test("rejects malformed UTF-8 instead of replacing chart bytes", async () => {
    await expect(downloads.readText(file(new Uint8Array([0xc3, 0x28])))).rejects.toMatchObject({
      kind: "validation",
      code: "invalid_chart_download",
    });
  });
  test("classifies a lost file session without retrying the consumed handle", async () => {
    let reads = 0;
    const chart = new ReceivedFile("chart.adofai", "application/json", 2, () => {
      reads++;
      throw new Error("Session disconnected");
    });
    await expect(downloads.readText(chart)).rejects.toMatchObject({
      kind: "connection",
      code: "chart_download_failed",
    });
    expect(reads).toBe(1);
  });
});
