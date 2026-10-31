import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import type { RenderApi } from "@/api/render/render-api";
import { orbitVersionCompatibility } from "@/models/render/orbit-version";
import {
  defaultRenderOptions,
  type RenderJob,
  type RenderOptions,
  type RenderSettings,
  renderJobFinished,
} from "@/models/render/render-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import {
  renderJobSchema,
  renderOptionsSchema,
  renderSettingsSchema,
} from "@/schemas/render/render-schema";
import { ApiError } from "@/shared/errors/api-error";

function toRenderRequest(options: RenderOptions) {
  return {
    width: options.width,
    height: options.height,
    videoFps: options.videoFps,
    targetFps: options.simulationFps,
    bitrateMbps: options.bitrateMbps,
    videoCodec: options.videoCodec,
    bitDepth: options.bitDepth,
    captureAudio: options.captureAudio,
    audioGainDb: options.audioGainDb,
    endDelaySeconds: options.endDelaySeconds,
    bgaMode: options.bgaMode,
    showPlanetRings: options.showPlanetRings,
    showSongTitle: options.showSongTitle,
    showCountdown: options.showCountdown,
    showResultText: options.showResultText,
    showHitJudgments: options.showHitJudgments,
    includeWebcam: options.includeWebcam,
    includeMicrophone: options.includeMicrophone,
  };
}

function synthSettings(): RenderSettings {
  return {
    defaults: defaultRenderOptions,
    outputDirectory: "",
    capabilities: {
      codecs: ["H264", "H265", "VP9", "AV1", "ProRes"],
      encoders: ["Auto", "Software", "NvidiaNvenc", "IntelQsv", "AmdAmf"],
      bitDepths: [8, 10],
      proResProfiles: ["HQ"],
      pixelFormats: ["auto"],
    },
  };
}

function unavailableHealth() {
  return {
    available: false,
    version: "",
    schemaVersion: 1 as const,
    busy: false,
    dmNoteConfigured: false,
    orbitAvailable: false,
  };
}

export function createRenderApi(clients: LocalAppChannels): RenderApi {
  const readRendererState = async () =>
    (await callRecorder(clients, (mod) => mod.readDownloadsState())).renderer;
  const readJob = async (): Promise<RenderJob | null> => {
    const value = await callRecorder(clients, (mod) => mod.readRenderState());
    return value ? decodeRecorder(renderJobSchema, value) : null;
  };
  return {
    async getHealth() {
      let renderer: Awaited<ReturnType<typeof readRendererState>>;
      try {
        renderer = await readRendererState();
      } catch {
        return unavailableHealth();
      }
      const job = await readJob().catch(() => null);
      const compatibility = orbitVersionCompatibility(renderer.version);
      return {
        available: true,
        version: renderer.version ?? "",
        schemaVersion: 1 as const,
        busy: job ? !renderJobFinished(job) : false,
        dmNoteConfigured: false,
        orbitAvailable: renderer.status === "ready" && compatibility === "supported",
      };
    },
    getSettings: async () => renderSettingsSchema.parse(synthSettings()),
    updateSettings: async () => renderSettingsSchema.parse(synthSettings()),
    chooseOutputDirectory: async () => {
      throw new ApiError("The output folder is controlled by OrbitRender settings.", {
        kind: "domain",
        code: "output_directory_unavailable",
      });
    },
    cancelOutputDirectorySelection: async () => ({ cancelled: true }),
    openOutputDirectory: async () => ({ opened: false }),
    async startRender(runId, options, levelPath) {
      const validated = renderOptionsSchema.parse(options);
      return decodeRecorder(
        renderJobSchema,
        await callRecorder(clients, (mod) =>
          mod.startRender({
            runId,
            ...(levelPath ? { levelPath } : {}),
            options: toRenderRequest(validated),
          }),
        ),
      );
    },
    readRenderState: readJob,
    async cancelRender(jobId) {
      return decodeRecorder(
        renderJobSchema,
        await callRecorder(clients, (mod) => mod.cancelRender({ jobId })),
      );
    },
    async prepareDownload() {
      throw new ApiError("Downloads are not used by the OrbitRender integration.", {
        kind: "domain",
        code: "download_unavailable",
      });
    },
  };
}
