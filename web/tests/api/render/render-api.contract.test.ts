import { describe, expect, test } from "bun:test";
import { createRenderApi } from "@/api/render/create-render-api";
import { createRenderApiMock } from "@/mocks/render/create-render-api-mock";
import { availableEncoders, defaultRenderOptions } from "@/models/render/render-model";
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
  test("remembers the chosen settings mode and quality with validated, independent snapshots", async () => {
    const calls: unknown[] = [];
    const mock = createRenderApiMock();
    const api = createRenderApi(
      clients(
        () => bundle,
        (method, params) => {
          calls.push({ method, params });
          return mock.getSettings();
        },
      ),
    );
    await api.updateSettings(defaultRenderOptions, { mode: "recommended", quality: "low" });
    expect(calls).toEqual([
      {
        method: "settings.update",
        params: { ...defaultRenderOptions, preferences: { mode: "recommended", quality: "low" } },
      },
    ]);
    expect(() => api.updateSettings({}, { mode: "advanced", quality: "ultra" } as never)).toThrow();
    expect(calls).toHaveLength(1);
    await mock.updateSettings({ width: 2560, height: 1440 }, { mode: "advanced", quality: "high" });
    const saved = await mock.getSettings();
    expect(saved.preferences).toEqual({ mode: "advanced", quality: "high" });
    if (saved.preferences) saved.preferences.mode = "recommended";
    expect((await mock.getSettings()).preferences?.mode).toBe("advanced");
  });
  test("exports the chosen matching level without starting a replay or saving its path as a default", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients(
        (method, params) => {
          calls.push({ method, params });
          return bundle;
        },
        () => {
          throw new Error("Choosing a level must not call the render engine");
        },
      ),
    );
    await api.exportBundle("run-1", defaultRenderOptions, "/levels/visual edit.adofai");
    await api.exportBundle("run-2", defaultRenderOptions);
    expect(calls).toEqual([
      {
        method: "replay.render-bundle.export",
        params: {
          runId: "run-1",
          levelPath: "/levels/visual edit.adofai",
          includeWebcam: true,
          includeMicrophone: true,
        },
      },
      {
        method: "replay.render-bundle.export",
        params: { runId: "run-2", includeWebcam: true, includeMicrophone: true },
      },
    ]);
  });
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
  test("preserves a cancellable focus wait and accepts older renderer status without it", async () => {
    let response = { ...job, state: "preparing", waitingForGameFocus: true };
    const api = createRenderApi(
      clients(
        () => bundle,
        () => response,
      ),
    );
    expect((await api.getStatus("render-1")).waitingForGameFocus).toBe(true);
    response = { ...response, state: "cancelled", waitingForGameFocus: false };
    expect((await api.cancel("render-1")).state).toBe("cancelled");
    const olderApi = createRenderApi(
      clients(
        () => bundle,
        () => job,
      ),
    );
    expect((await olderApi.getStatus("render-1")).waitingForGameFocus).toBe(false);
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
      { ...defaultRenderOptions, videoFps: 0 },
      { ...defaultRenderOptions, simulationFps: 30 },
      { ...defaultRenderOptions, encoder: "NvidiaNvenc", crf: 23 },
      { ...defaultRenderOptions, crf: 52 },
      { ...defaultRenderOptions, videoCodec: "VP9", encoder: "NvidiaNvenc" },
      { ...defaultRenderOptions, videoCodec: "VP9", encoder: "Auto" },
      { ...defaultRenderOptions, pixelFormat: "bgra" },
      { ...defaultRenderOptions, bitDepth: 10, pixelFormat: "yuv420p" },
      { ...defaultRenderOptions, width: Number.NaN },
    ]) {
      expect(() => api.start(bundle.manifestPath, options as never)).toThrow();
    }
    expect(called).toBe(false);
  });
  test("chooses save folders asynchronously and opens only an owned completed job", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients(
        () => bundle,
        (method, params) => {
          calls.push({ method, params });
          if (method === "output-directory.selection.cancel") return { cancelled: true };
          if (method === "output-directory.open") return { opened: true };
          return {
            selectionId: "pick-1",
            pending: method === "output-directory.choose",
            outputDirectory: method === "output-directory.choose" ? null : "/local/videos",
          };
        },
      ),
    );
    expect((await api.chooseOutputDirectory("/local/start")).pending).toBe(true);
    expect((await api.getOutputDirectorySelection("pick-1")).outputDirectory).toBe("/local/videos");
    await api.cancelOutputDirectorySelection("pick-1");
    await api.openOutputDirectory("render-1");
    expect(calls).toEqual([
      { method: "output-directory.choose", params: { initialPath: "/local/start" } },
      { method: "output-directory.selection.get", params: { selectionId: "pick-1" } },
      { method: "output-directory.selection.cancel", params: { selectionId: "pick-1" } },
      { method: "output-directory.open", params: { jobId: "render-1" } },
    ]);
  });
  test("retains the precise invalid field and row returned by the renderer", async () => {
    const api = createRenderApi(
      clients(
        () => bundle,
        () => ({
          ...job,
          state: "failed",
          errorCode: "render_csv_value_invalid",
          errorMessage: "Invalid time",
          errorDetails: { field: "timeUs", line: 2, file: "inputs.csv" },
        }),
      ),
    );
    expect(await api.getStatus("render-1")).toMatchObject({
      errorCode: "render_csv_value_invalid",
      errorDetails: { field: "timeUs", line: 2, file: "inputs.csv" },
    });
  });
  test("retains option error details and does not retry permanent namespace errors as a connection", async () => {
    const api = createRenderApi(
      clients(
        () => bundle,
        () => ({
          error: {
            code: "render_option_invalid",
            message: "Unsupported encoder",
            details: { field: "encoder" },
          },
        }),
      ),
    );
    await expect(api.start(bundle.manifestPath, defaultRenderOptions)).rejects.toMatchObject({
      kind: "domain",
      code: "render_option_invalid",
      cause: { details: { field: "encoder" } },
    });
    const unavailable = createRenderApi(
      clients(
        () => bundle,
        () => {
          throw Object.assign(new Error("Missing renderer namespace"), {
            name: "IpcResponseError",
            code: "namespace_not_found",
          });
        },
      ),
    );
    await expect(unavailable.getHealth()).rejects.toMatchObject({
      kind: "protocol",
      code: "namespace_not_found",
    });
  });
  test("uses platform-specific engine encoder capabilities", async () => {
    const settings = {
      defaults: defaultRenderOptions,
      outputDirectory: "/local/videos",
      capabilities: {
        codecs: ["H264", "ProRes"],
        encoders: ["Auto", "Software", "AppleVideoToolbox"],
        bitDepths: [8, 10],
        proResProfiles: ["Standard"],
        pixelFormats: ["auto"],
      },
      engineOptions: { codecs: [{ value: "ProRes", encoders: ["Software"] }] },
    };
    const api = createRenderApi(
      clients(
        () => bundle,
        () => settings,
      ),
    );
    const loaded = await api.getSettings();
    expect(availableEncoders("ProRes", loaded.capabilities.encoders, loaded.engineOptions)).toEqual(
      ["Software"],
    );
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
