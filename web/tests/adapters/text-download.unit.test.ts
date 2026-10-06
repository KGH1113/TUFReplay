import { describe, expect, test } from "bun:test";
import { createLocalTextDownloads } from "@/adapters/adofai-ipc/text-download";

const url = "http://127.0.0.1:32145/ipc/download/chart-ticket";
const encode = (text: string) => new TextEncoder().encode(text);

function fetcher(handle: (input: string, options: RequestInit) => Promise<Response>) {
  return handle as unknown as typeof fetch;
}

describe("local chart download adapter", () => {
  test("decodes a large UTF-8 chart across arbitrary chunk boundaries", async () => {
    const expected = `{"comment":"${'한🙂"'.repeat(500_000)}"}`;
    const bytes = encode(expected);
    let offset = 0;
    const downloads = createLocalTextDownloads(
      fetcher(async (input, options) => {
        expect(input).toBe(url);
        expect(options).toMatchObject({
          cache: "no-store",
          credentials: "omit",
          redirect: "error",
        });
        return new Response(
          new ReadableStream<Uint8Array>({
            pull(controller) {
              if (offset === bytes.length) return controller.close();
              const end = Math.min(bytes.length, offset + 8191);
              controller.enqueue(bytes.slice(offset, end));
              offset = end;
            },
          }),
        );
      }),
    );
    expect(bytes.length).toBeGreaterThan(2 * 1024 * 1024);
    expect(await downloads.readText({ url, byteLength: bytes.length })).toBe(expected);
  });

  test("rejects unsafe ticket URLs before making a request", async () => {
    let requests = 0;
    const downloads = createLocalTextDownloads(
      fetcher(async () => {
        requests++;
        return new Response("{}");
      }),
    );
    for (const unsafe of [
      "not a URL",
      "http://example.com/ipc/download/ticket",
      "http://127.0.0.1:32144/ipc/download/ticket",
      "http://127.0.0.1:32145/other/ticket",
      `${url}?secret=1`,
      `${url}#fragment`,
      "http://user:pass@127.0.0.1:32145/ipc/download/ticket",
    ])
      await expect(downloads.readText({ url: unsafe, byteLength: 2 })).rejects.toMatchObject({
        kind: "validation",
        code: "invalid_chart_download",
      });
    expect(requests).toBe(0);
  });

  test("rejects truncated or oversized content and cancels excess bytes", async () => {
    let cancelled = false;
    const excessive = createLocalTextDownloads(
      fetcher(
        async () =>
          new Response(
            new ReadableStream<Uint8Array>({
              start(controller) {
                controller.enqueue(encode("too much data"));
              },
              cancel() {
                cancelled = true;
              },
            }),
          ),
      ),
    );
    await expect(excessive.readText({ url, byteLength: 2 })).rejects.toMatchObject({
      kind: "validation",
    });
    expect(cancelled).toBe(true);
    const truncated = createLocalTextDownloads(fetcher(async () => new Response("{}")));
    await expect(truncated.readText({ url, byteLength: 10 })).rejects.toMatchObject({
      kind: "validation",
    });
  });

  test("propagates cancellation to the download request", async () => {
    const cancellation = new AbortController();
    const downloads = createLocalTextDownloads(
      fetcher(async (_input, options) => {
        cancellation.abort();
        expect(options.signal?.aborted).toBe(true);
        throw new DOMException("Download cancelled", "AbortError");
      }),
    );
    await expect(
      downloads.readText({ url, byteLength: 2 }, { signal: cancellation.signal }),
    ).rejects.toMatchObject({
      name: "AbortError",
    });
  });

  test("rejects malformed UTF-8 instead of replacing chart bytes", async () => {
    const downloads = createLocalTextDownloads(
      fetcher(async () => new Response(new Uint8Array([0xc3, 0x28]))),
    );
    await expect(downloads.readText({ url, byteLength: 2 })).rejects.toMatchObject({
      kind: "validation",
      code: "invalid_chart_download",
    });
  });

  test("classifies HTTP failures without closing the message session", async () => {
    const downloads = createLocalTextDownloads(
      fetcher(async () => new Response("", { status: 404 })),
    );
    await expect(downloads.readText({ url, byteLength: 2 })).rejects.toMatchObject({
      kind: "http",
      code: "chart_download_failed",
    });
  });
});
