import { afterEach, expect, test } from "bun:test";
import { adofaiIpcFetch } from "@/shared/clients/adofai-ipc-fetch";

const originalFetch = globalThis.fetch;
afterEach(() => {
  globalThis.fetch = originalFetch;
});

test("IPC JSON preserves Korean names and preset text through the listener's charset decoder", async () => {
  const params = { name: "한글 키뷰어 🎹", presetJson: JSON.stringify({ label: "왼쪽 시프트" }) };
  globalThis.fetch = (async (input, init) => {
    const request = new Request(input, init);
    const charset =
      /charset=([^;]+)/i.exec(request.headers.get("content-type") ?? "")?.[1] ?? "ascii";
    expect(charset).toBe("utf-8");
    const decoded = new TextDecoder(charset).decode(await request.arrayBuffer());
    expect(JSON.parse(decoded).params).toEqual(params);
    return Response.json({ Ok: true, Result: params, Id: "import" });
  }) as typeof fetch;
  const response = await adofaiIpcFetch("http://localhost:32145/ipc", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ namespace: "tuf-replay", method: "visual.presets.import", params }),
  });
  expect(await response.json()).toEqual({ ok: true, result: params, id: "import" });
});

test("IPC Request input keeps its body, cancellation and headers", async () => {
  const controller = new AbortController();
  const request = new Request("http://localhost:32145/ipc", {
    method: "POST",
    headers: { "content-type": "application/json", "x-test": "retained" },
    body: JSON.stringify({ name: "오버레이" }),
    signal: controller.signal,
  });
  globalThis.fetch = (async (input, init) => {
    const sent = new Request(input, init);
    expect(sent.headers.get("x-test")).toBe("retained");
    expect(sent.headers.get("content-type")).toBe("application/json; charset=utf-8");
    expect(await sent.json()).toEqual({ name: "오버레이" });
    controller.abort();
    expect(sent.signal.aborted).toBe(true);
    return Response.json({ ok: true });
  }) as typeof fetch;
  await adofaiIpcFetch(request);
});

test("non-IPC requests pass through unchanged", async () => {
  const init = { method: "POST", headers: { "content-type": "application/json" }, body: "{}" };
  globalThis.fetch = (async (_input, received) => {
    expect(received).toBe(init);
    return new Response("ok");
  }) as typeof fetch;
  expect(await (await adofaiIpcFetch("https://example.com/api", init)).text()).toBe("ok");
});
