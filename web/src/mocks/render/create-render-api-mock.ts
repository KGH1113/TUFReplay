import type { RenderApi } from "@/api/render/render-api";
import {
  defaultRenderOptions,
  type RenderJob,
  type RenderOptions,
  type RenderSettings,
} from "@/models/render/render-model";

export function createRenderApiMock(): RenderApi {
  let defaults = { ...defaultRenderOptions, outputDirectory: "/Users/example/Videos/TUFReplay" };
  let currentJob: RenderJob | null = null;
  let renderStep = 0;
  let exportCancelled = false;
  let pickerCancelled = false;
  const settings = (): RenderSettings => ({
    defaults: { ...defaults },
    outputDirectory: defaults.outputDirectory,
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
  const bundle = (runId = "mock-run") => ({
    jobId: "mock-export",
    runId,
    state: exportCancelled ? ("cancelled" as const) : ("completed" as const),
    progress: 1,
    manifestPath: exportCancelled ? null : "/mock/manifest.json",
    errorCode: null,
    errorMessage: null,
    errorDetails: null,
  });
  return {
    async getHealth() {
      return {
        available: true,
        version: "mock",
        schemaVersion: 1,
        busy: Boolean(currentJob && !["completed", "cancelled"].includes(currentJob.state)),
        dmNoteConfigured: true,
        orbitAvailable: true,
      };
    },
    async getSettings() {
      return settings();
    },
    async updateSettings(options) {
      defaults = { ...defaults, ...options };
      return settings();
    },
    async chooseOutputDirectory() {
      pickerCancelled = false;
      return { selectionId: "mock-folder", pending: true, outputDirectory: null };
    },
    async getOutputDirectorySelection() {
      return {
        selectionId: "mock-folder",
        pending: false,
        outputDirectory: pickerCancelled ? null : "/Users/example/Videos/TUFReplay",
      };
    },
    async cancelOutputDirectorySelection() {
      pickerCancelled = true;
      return { cancelled: true };
    },
    async openOutputDirectory() {
      return { opened: true };
    },
    async exportBundle(runId) {
      exportCancelled = false;
      return { ...bundle(runId), state: "preparing", progress: 0, manifestPath: null };
    },
    async getExportStatus() {
      return bundle();
    },
    async cancelExport() {
      exportCancelled = true;
      return bundle();
    },
    async start(_manifestPath: string, options: RenderOptions) {
      renderStep = 0;
      const extension =
        options.videoCodec === "ProRes" ? ".mov" : options.videoCodec === "VP9" ? ".webm" : ".mp4";
      currentJob = {
        jobId: "mock-render",
        state: "preparing",
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
      return { ...currentJob };
    },
    async getStatus() {
      if (!currentJob) throw new Error("Start a mock render first.");
      if (currentJob.state !== "cancelled") {
        renderStep++;
        currentJob.state =
          renderStep >= 4 ? "completed" : renderStep >= 3 ? "compositing" : "rendering";
        currentJob.progress = Math.min(1, renderStep / 4);
        currentJob.canOpenOutput = currentJob.state === "completed";
      }
      return { ...currentJob };
    },
    async cancel() {
      if (!currentJob) throw new Error("Start a mock render first.");
      currentJob.state = "cancelled";
      return { ...currentJob };
    },
    async prepareDownload() {
      return "http://127.0.0.1:32145/ipc/download/mock-render";
    },
  };
}
