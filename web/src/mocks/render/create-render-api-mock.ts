import type { AppEventMap } from "@/api/app-events";
import type { RenderApi } from "@/api/render/render-api";
import {
  defaultRenderOptions,
  type RenderJob,
  type RenderOptions,
  type RenderPreferences,
  type RenderSettings,
} from "@/models/render/render-model";

export function createRenderApiMock(
  emit: <K extends keyof AppEventMap>(name: K, state: AppEventMap[K]) => void = () => {},
): RenderApi {
  let defaults = { ...defaultRenderOptions, outputDirectory: "/Users/example/Videos/TUFReplay" };
  let preferences: RenderPreferences = { mode: "recommended", quality: null };
  let currentJob: RenderJob | null = null;
  let renderStep = 0;
  let renderTimer: ReturnType<typeof setInterval> | undefined;
  const settings = (): RenderSettings => ({
    defaults: { ...defaults },
    preferences: { ...preferences },
    outputDirectory: defaults.outputDirectory,
    system: {
      platform: "windows",
      processorName: "AMD Ryzen 7 7800X3D",
      logicalProcessors: 16,
      memoryMb: 32768,
      graphicsName: "NVIDIA GeForce RTX 4070",
      graphicsMemoryMb: 12288,
      maxTextureSize: 16384,
      encoding: { state: "ready", h264Encoders: ["Software", "NvidiaNvenc"] },
    },
    capabilities: {
      codecs: ["H264", "H265", "VP9", "AV1", "ProRes"],
      encoders: ["Auto", "Software", "NvidiaNvenc", "IntelQsv", "AmdAmf", "AppleVideoToolbox"],
      bitDepths: [8, 10],
      proResProfiles: ["Proxy", "LT", "Standard", "HQ", "FourFourFourFour", "FourFourFourFourXQ"],
      pixelFormats: [
        "auto",
        "yuv420p",
        "yuv420p10le",
        "yuv422p10le",
        "yuva444p10le",
        "p210le",
        "bgra",
      ],
    },
  });
  return {
    async getHealth() {
      return {
        available: true,
        version: "mock",
        schemaVersion: 1,
        busy: Boolean(
          currentJob && !["completed", "failed", "cancelled"].includes(currentJob.state),
        ),
        dmNoteConfigured: true,
        orbitAvailable: true,
        takesBitrateKbps: true,
      };
    },
    async getSettings() {
      return settings();
    },
    async updateSettings(options, selectedPreferences) {
      defaults = { ...defaults, ...options };
      if (selectedPreferences) preferences = { ...selectedPreferences };
      const state = settings();
      emit("renderer.settings.changed", state);
      return state;
    },
    async chooseOutputDirectory() {
      return {
        selectionId: "mock-folder",
        pending: false,
        outputDirectory: "/Users/example/Videos/TUFReplay",
      };
    },
    async cancelOutputDirectorySelection() {
      return { cancelled: true };
    },
    async openOutputDirectory() {
      return { opened: true };
    },
    async startRender(_runId: string, options: RenderOptions) {
      renderStep = 0;
      const extension =
        options.videoCodec === "ProRes" ? ".mov" : options.videoCodec === "VP9" ? ".webm" : ".mp4";
      currentJob = {
        jobId: "mock-render",
        state: "exporting",
        stage: "exporting_recording",
        waitingForGameFocus: false,
        progress: 0,
        warnings: [],
        errorCode: null,
        errorMessage: null,
        errorDetails: null,
        outputFile: `TUFReplay-mock${extension}`,
        localOutputPath: `${options.outputDirectory}/TUFReplay-mock${extension}`,
        outputDirectory: options.outputDirectory,
        canOpenOutput: false,
        canDownload: false,
      };
      clearInterval(renderTimer);
      renderTimer = setInterval(() => {
        if (!currentJob || currentJob.state === "cancelled") {
          clearInterval(renderTimer);
          return;
        }
        renderStep++;
        currentJob.state =
          renderStep >= 4 ? "completed" : renderStep >= 3 ? "compositing" : "rendering";
        currentJob.progress = Math.min(1, renderStep / 4);
        currentJob.canOpenOutput = currentJob.state === "completed";
        emit("render-state.changed", { ...currentJob });
        if (currentJob.state === "completed") clearInterval(renderTimer);
      }, 250);
      return { ...currentJob };
    },
    async openRenderFile() {
      return true;
    },
    async revealRenderFile() {
      return true;
    },
    async readRenderVideo() {
      // A 4K ProRes render is a QuickTime file of about 10 GB, which most browsers cannot play.
      if (currentJob?.outputFile?.endsWith(".mov"))
        return {
          name: "TUFReplay-mock.mov",
          type: "video/quicktime",
          size: 9_951_246_926,
          stream: () => {
            throw new Error("The mock ProRes video has no content.");
          },
        };
      const video = await sampleVideo();
      return {
        name: "TUFReplay-mock.webm",
        type: video.type,
        size: video.size,
        stream: () => video.stream(),
        // Stands in for the game's streaming URL.
        async open() {
          const url = URL.createObjectURL(video);
          return { url, close: async () => URL.revokeObjectURL(url) };
        },
      };
    },
    async readRenderState() {
      return currentJob ? { ...currentJob } : null;
    },
    async cancelRender() {
      if (!currentJob) throw new Error("Start a mock render first.");
      currentJob.state = "cancelled";
      clearInterval(renderTimer);
      emit("render-state.changed", { ...currentJob });
      return { ...currentJob };
    },
    async prepareDownload() {
      return "http://127.0.0.1:32145/ipc/download/mock-render";
    },
  };
}

/**
 * A two-second clip drawn in the browser, so the finished-video preview can be
 * reviewed without a game. Falls back to an error where recording is unsupported.
 */
async function sampleVideo(): Promise<Blob> {
  if (typeof MediaRecorder === "undefined") throw new Error("No video recorder in this browser.");
  const canvas = document.createElement("canvas");
  canvas.width = 640;
  canvas.height = 360;
  const context = canvas.getContext("2d");
  if (!context) throw new Error("No canvas in this browser.");
  const recorder = new MediaRecorder(canvas.captureStream(30), { mimeType: "video/webm" });
  const chunks: Blob[] = [];
  recorder.ondataavailable = (event) => chunks.push(event.data);
  const stopped = new Promise<void>((resolve) => {
    recorder.onstop = () => resolve();
  });
  const start = performance.now();
  recorder.start();
  await new Promise<void>((resolve) => {
    const draw = (now: number) => {
      const t = (now - start) / 1000;
      context.fillStyle = "#101010";
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.fillStyle = "#b8f03c";
      context.beginPath();
      context.arc(
        320 + Math.cos(t * Math.PI * 2) * 120,
        180 + Math.sin(t * Math.PI * 2) * 120,
        18,
        0,
        Math.PI * 2,
      );
      context.fill();
      if (t < 2) requestAnimationFrame(draw);
      else resolve();
    };
    requestAnimationFrame(draw);
  });
  recorder.stop();
  await stopped;
  return new Blob(chunks, { type: "video/webm" });
}
