import { describe, expect, test } from "bun:test";
import { createRenderApi } from "@/api/render/create-render-api";
import { defaultRenderOptions } from "@/models/render/render-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import type { OrbitRenderClient, OrbitRenderJob } from "@/ports/orbit-render-client";
import { scriptedChannels } from "../../fixtures/local-message-peer";

const bundle = {
  jobId: "export-1",
  runId: "run-1",
  state: "completed" as const,
  progress: 1,
  manifestPath: "/tmp/bundle/manifest.json",
  errorCode: null,
  errorMessage: null,
};

const orbitJob: OrbitRenderJob = {
  id: "render-1",
  state: "Rendering",
  progress: 0.5,
  outputPath: null,
  error: null,
};

function clients(
  tuf: (method: string, params: unknown) => unknown,
  orbit?: Partial<OrbitRenderClient>,
): LocalAppChannels {
  return {
    ...scriptedChannels(tuf),
    orbit: {
      ready: async () => {},
      health: async () => ({ ok: true, renderer: "Idle" }),
      createRender: async () => ({ id: orbitJob.id }),
      renderStatus: async () => orbitJob,
      cancelRender: async () => ({}),
      ...orbit,
    },
  };
}

describe("OrbitRender render IPC contract", () => {
  test("maps OrbitRender health to the render dialog state", async () => {
    const idle = createRenderApi(clients(() => bundle));
    expect(await idle.getHealth()).toMatchObject({
      available: true,
      orbitAvailable: true,
      busy: false,
    });

    const busy = createRenderApi(
      clients(() => bundle, { health: async () => ({ ok: true, renderer: "Rendering" }) }),
    );
    expect((await busy.getHealth()).busy).toBe(true);

    const missing = createRenderApi({ ...scriptedChannels(() => bundle) });
    expect(await missing.getHealth()).toEqual({
      available: false,
      version: "",
      schemaVersion: 1,
      busy: false,
      dmNoteConfigured: false,
      orbitAvailable: false,
    });
  });

  test("always returns settings shaped for the existing options form", async () => {
    const api = createRenderApi(clients(() => bundle));
    const settings = await api.getSettings();
    expect(settings.defaults.width).toBe(defaultRenderOptions.width);
    expect(settings.capabilities.codecs).toContain("H264");
    expect(settings.capabilities.codecs).toContain("ProRes");
    expect(settings.outputDirectory).toBe("");
  });

  test("exports the bundle through TUF and starts OrbitRender for that manifest", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients(
        (method, params) => {
          calls.push({ target: "tuf", method, params });
          return bundle;
        },
        {
          createRender: async (request) => {
            calls.push({ target: "orbit", method: "createRender", params: request });
            return { id: "render-1" };
          },
        },
      ),
    );
    const exported = await api.exportBundle("run-1", defaultRenderOptions);
    const started = await api.start(exported.manifestPath!, {
      ...defaultRenderOptions,
      width: 2560,
      height: 1440,
      simulationFps: 480,
    });

    expect(started).toMatchObject({
      jobId: "render-1",
      state: "rendering",
      progress: 0.5,
      canOpenOutput: false,
    });
    expect(calls).toEqual([
      {
        target: "tuf",
        method: "prepareRenderBundle",
        params: { runId: "run-1", includeWebcam: true, includeMicrophone: true },
      },
      {
        target: "orbit",
        method: "createRender",
        params: {
          options: {
            replayManifestPath: bundle.manifestPath,
            width: 2560,
            height: 1440,
            targetFps: 480,
            videoFps: defaultRenderOptions.videoFps,
            bitrateMbps: defaultRenderOptions.bitrateMbps,
            videoCodec: defaultRenderOptions.videoCodec,
            bitDepth: defaultRenderOptions.bitDepth,
            captureAudio: defaultRenderOptions.captureAudio,
            audioGainDb: defaultRenderOptions.audioGainDb,
            endDelaySeconds: defaultRenderOptions.endDelaySeconds,
            bgaMode: defaultRenderOptions.bgaMode,
            showPlanetRings: defaultRenderOptions.showPlanetRings,
            showSongTitle: defaultRenderOptions.showSongTitle,
            showCountdown: defaultRenderOptions.showCountdown,
            showResultText: defaultRenderOptions.showResultText,
            showHitJudgments: defaultRenderOptions.showHitJudgments,
            showModOverlay: true,
          },
        },
      },
    ]);
  });

  test("maps OrbitRender job states and forwards cancellation", async () => {
    const calls: unknown[] = [];
    const states: OrbitRenderJob[] = [
      { ...orbitJob, state: "Preparing" },
      { ...orbitJob, state: "Finishing" },
      { ...orbitJob, state: "Completed", outputPath: "/videos/out.mp4", progress: 1 },
    ];
    const api = createRenderApi(
      clients(
        () => bundle,
        {
          cancelRender: async (request) => {
            calls.push({ method: "cancelRender", request });
            return {};
          },
          renderStatus: async () => states.shift() ?? orbitJob,
        },
      ),
    );
    expect((await api.getStatus("render-1")).state).toBe("preparing");
    expect((await api.getStatus("render-1")).state).toBe("compositing");
    const completed = await api.getStatus("render-1");
    expect(completed).toMatchObject({
      state: "completed",
      progress: 1,
      outputFile: "/videos/out.mp4",
    });
    await api.cancel("render-1");
    expect(calls).toEqual([{ method: "cancelRender", request: { id: "render-1" } }]);
  });

  test("rejects invalid options before contacting OrbitRender", async () => {
    let called = false;
    const api = createRenderApi(
      clients(
        () => bundle,
        {
          createRender: async () => {
            called = true;
            return { id: "render-1" };
          },
        },
      ),
    );
    for (const options of [
      { ...defaultRenderOptions, width: 1919 },
      { ...defaultRenderOptions, videoFps: 0 },
      { ...defaultRenderOptions, simulationFps: 30 },
    ]) {
      await expect(api.start(bundle.manifestPath, options as never)).rejects.toBeDefined();
    }
    expect(called).toBe(false);
  });

  test("preserves export capability errors and reports missing OrbitRender", async () => {
    const api = createRenderApi(
      clients(
        () => ({
          error: { code: "render_recording_incompatible", message: "Missing accepted judgments" },
        }),
        {
          createRender: async () => {
            throw new Error("OrbitRender is not connected.");
          },
        },
      ),
    );
    await expect(api.exportBundle("run-1", defaultRenderOptions)).rejects.toMatchObject({
      kind: "domain",
      code: "render_recording_incompatible",
    });
    await expect(api.start(bundle.manifestPath, defaultRenderOptions)).rejects.toBeDefined();
  });
});
