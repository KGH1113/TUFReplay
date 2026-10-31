import { describe, expect, test } from "bun:test";
import { createRenderApi } from "@/api/render/create-render-api";
import { defaultRenderOptions } from "@/models/render/render-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { scriptedChannels } from "../../fixtures/local-message-peer";

const job = {
  jobId: "job-1",
  runId: "run-1",
  state: "rendering" as const,
  stage: "rendering",
  progress: 0.5,
  outputFile: null,
  warnings: [],
  waitingForGameFocus: false,
  errorCode: null,
  errorMessage: null,
  errorDetails: null,
};

function clients(
  tuf: (method: string, params: unknown) => unknown,
  options: { version?: string | null; status?: string; currentJob?: unknown } = {},
): LocalAppChannels {
  const version = options.version === undefined ? "2.7.0" : options.version;
  const status = options.status ?? "ready";
  const currentJob = options.currentJob ?? null;
  return scriptedChannels((method, params) => {
    if (method === "readDownloadsState")
      return { renderer: { status, version }, ffmpeg: {}, cameraNeedsFfmpeg: false };
    if (method === "readRenderState") return currentJob;
    return tuf(method, params);
  });
}

describe("TUFReplay render IPC contract", () => {
  test("reports OrbitRender availability from the installed version", async () => {
    for (const version of ["2.6.9", "1.99.99", null]) {
      const api = createRenderApi(clients(() => job, { version }));
      expect(await api.getHealth()).toMatchObject({
        available: true,
        orbitAvailable: false,
      });
    }
    for (const version of ["2.7.0", "2.8.1", "3.0.0"]) {
      const health = await createRenderApi(clients(() => job, { version })).getHealth();
      expect(health).toMatchObject({ available: true, orbitAvailable: true, version });
    }
    const notReady = createRenderApi(clients(() => job, { status: "missing" }));
    expect((await notReady.getHealth()).orbitAvailable).toBe(false);
  });

  test("marks a running job as busy and a finished job as idle", async () => {
    const running = createRenderApi(clients(() => job, { currentJob: job }));
    expect((await running.getHealth()).busy).toBe(true);
    const finished = createRenderApi(
      clients(() => job, { currentJob: { ...job, state: "completed", progress: 1 } }),
    );
    expect((await finished.getHealth()).busy).toBe(false);
  });

  test("returns an unavailable health snapshot when the recorder is not connected", async () => {
    const api = createRenderApi(
      scriptedChannels(() => {
        throw new Error("The game is not running.");
      }),
    );
    expect(await api.getHealth()).toEqual({
      available: false,
      version: "",
      schemaVersion: 1,
      busy: false,
      dmNoteConfigured: false,
      orbitAvailable: false,
    });
  });

  test("always returns settings shaped for the existing options form", async () => {
    const api = createRenderApi(clients(() => job));
    const settings = await api.getSettings();
    expect(settings.defaults.width).toBe(defaultRenderOptions.width);
    expect(settings.capabilities.codecs).toContain("H264");
    expect(settings.capabilities.codecs).toContain("ProRes");
    expect(settings.outputDirectory).toBe("");
  });

  test("starts one TUF render job and forwards the level and render options", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients((method, params) => {
        calls.push({ method, params });
        return { ...job, state: "exporting", stage: "exporting_recording", progress: 0 };
      }),
    );
    const started = await api.startRender("run-1", {
      ...defaultRenderOptions,
      width: 2560,
      height: 1440,
      simulationFps: 480,
    });

    expect(started).toMatchObject({ jobId: "job-1", state: "exporting", progress: 0 });
    expect(calls).toEqual([
      {
        method: "startRender",
        params: {
          runId: "run-1",
          options: {
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
            includeWebcam: defaultRenderOptions.includeWebcam,
            includeMicrophone: defaultRenderOptions.includeMicrophone,
          },
        },
      },
    ]);
  });

  test("forwards the chosen level override only when present", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients((method, params) => {
        calls.push({ method, params });
        return job;
      }),
    );
    await api.startRender("run-1", defaultRenderOptions, "/levels/other.adofai");
    expect(calls[0]).toMatchObject({
      method: "startRender",
      params: { runId: "run-1", levelPath: "/levels/other.adofai" },
    });
  });

  test("reads and cancels the single active render job", async () => {
    const calls: unknown[] = [];
    const api = createRenderApi(
      clients((method, params) => {
        calls.push({ method, params });
        return { ...job, state: "cancelled" };
      }),
    );
    const cancelled = await api.cancelRender("job-1");
    expect(cancelled.state).toBe("cancelled");
    expect(calls).toEqual([{ method: "cancelRender", params: { jobId: "job-1" } }]);
  });

  test("returns null when no render job exists", async () => {
    const api = createRenderApi(clients(() => job));
    expect(await api.readRenderState()).toBeNull();
  });

  test("rejects invalid options before contacting the game", async () => {
    let called = false;
    const api = createRenderApi(
      clients(() => {
        called = true;
        return job;
      }),
    );
    for (const options of [
      { ...defaultRenderOptions, width: 1919 },
      { ...defaultRenderOptions, videoFps: 0 },
      { ...defaultRenderOptions, simulationFps: 30 },
    ]) {
      await expect(api.startRender("run-1", options as never)).rejects.toBeDefined();
    }
    expect(called).toBe(false);
  });

  test("preserves TUFReplay domain errors", async () => {
    const api = createRenderApi(
      clients(() => ({
        error: { code: "renderer_missing", message: "Install and enable OrbitRender." },
      })),
    );
    await expect(api.startRender("run-1", defaultRenderOptions)).rejects.toMatchObject({
      kind: "domain",
      code: "renderer_missing",
    });
  });
});
