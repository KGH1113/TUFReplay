import { describe, expect, test } from "bun:test";
import { createRenderApi } from "@/api/render/create-render-api";
import { defaultRenderOptions } from "@/models/render/render-model";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";

const bundle = {
  jobId: "export-1",
  runId: "run-1",
  state: "completed",
  progress: 1,
  manifestPath: "/tmp/bundle/manifest.json",
  errorCode: null,
  errorMessage: null,
};
const job = {
  jobId: "render-1",
  state: "rendering",
  progress: 0.5,
  errorMessage: null,
  outputFile: null,
  canDownload: false,
};
function clients(
  tuf: (method: string, params: unknown) => unknown,
  render: (method: string, params: unknown) => unknown,
): AdofaiIpcClients {
  return {
    namespace: { call: async (method: string, params: unknown) => tuf(method, params) },
    rendererNamespace: { call: async (method: string, params: unknown) => render(method, params) },
  } as unknown as AdofaiIpcClients;
}
describe("neutral render IPC contract", () => {
  test("keeps bundle export in TUF and rendering in its independent namespace", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients(
        (method, params) => {
          calls.push({ namespace: "tuf-replay", method, params });
          return bundle;
        },
        (method, params) => {
          calls.push({ namespace: "tuf-replay-renderer", method, params });
          return job;
        },
      ),
    );
    await api.exportBundle("run-1", defaultRenderOptions);
    await api.start(bundle.manifestPath, defaultRenderOptions);
    await api.getExportStatus("export-1");
    await api.cancelExport("export-1");
    await api.getStatus("render-1");
    await api.cancel("render-1");
    expect(calls).toEqual([
      {
        namespace: "tuf-replay",
        method: "replay.render-bundle.export",
        params: { runId: "run-1", includeWebcam: true, includeMicrophone: true },
      },
      {
        namespace: "tuf-replay-renderer",
        method: "render.start",
        params: { manifestPath: bundle.manifestPath, ...defaultRenderOptions },
      },
      {
        namespace: "tuf-replay",
        method: "replay.render-bundle.status.get",
        params: { jobId: "export-1" },
      },
      {
        namespace: "tuf-replay",
        method: "replay.render-bundle.cancel",
        params: { jobId: "export-1" },
      },
      {
        namespace: "tuf-replay-renderer",
        method: "render.status.get",
        params: { jobId: "render-1" },
      },
      { namespace: "tuf-replay-renderer", method: "render.cancel", params: { jobId: "render-1" } },
    ]);
  });
  test("rejects unsupported output options before issuing a command", async () => {
    let called = false;
    const api = createRenderApi(
      clients(
        () => {
          called = true;
          return bundle;
        },
        () => {
          called = true;
          return job;
        },
      ),
    );
    for (const options of [
      { ...defaultRenderOptions, width: 1919 },
      { ...defaultRenderOptions, fps: 0 },
      { ...defaultRenderOptions, width: Number.NaN },
    ]) {
      expect(() => api.start(bundle.manifestPath, options as never)).toThrow();
    }
    expect(called).toBe(false);
  });
  test("preserves export capability errors", async () => {
    const api = createRenderApi(
      clients(
        () => ({
          error: { code: "render_recording_incompatible", message: "Missing accepted judgments" },
        }),
        () => job,
      ),
    );
    await expect(api.exportBundle("run-1", defaultRenderOptions)).rejects.toMatchObject({
      kind: "domain",
      code: "render_recording_incompatible",
    });
  });
  test("accepts one-use local downloads and rejects external or credential-bearing URLs", async () => {
    const allowed = "http://127.0.0.1:32145/ipc/download/render_1";
    expect(
      await createRenderApi(
        clients(
          () => bundle,
          () => ({ Url: allowed }),
        ),
      ).prepareDownload("render-1"),
    ).toBe(allowed);
    for (const url of [
      "https://example.com/video.mp4",
      "http://user@127.0.0.1:32145/ipc/download/render_1",
      "http://127.0.0.1:32145/ipc/download/render_1?other=1",
    ]) {
      await expect(
        createRenderApi(
          clients(
            () => bundle,
            () => ({ Url: url }),
          ),
        ).prepareDownload("render-1"),
      ).rejects.toMatchObject({ kind: "validation", code: "invalid_response" });
    }
  });
});
