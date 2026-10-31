import type { AdofaiIpcClients } from "@/api/domain-messages";
import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import type { RenderApi } from "@/api/render/render-api";
import { orbitVersionCompatibility } from "@/models/render/orbit-version";
import {
  defaultRenderOptions,
  type RenderJob,
  type RenderOptions,
  type RenderSettings,
} from "@/models/render/render-model";
import type { OrbitRenderClient } from "@/ports/orbit-render-client";
import {
  renderExportStatusSchema,
  renderOptionsSchema,
  renderSettingsSchema,
} from "@/schemas/render/render-schema";
import { ApiError } from "@/shared/errors/api-error";

const orbitStates = new Set([
  "queued",
  "loading",
  "preparing",
  "awaitingconfirmation",
  "rendering",
  "finishing",
  "completed",
  "failed",
  "cancelled",
]);

type OrbitJob = Awaited<ReturnType<NonNullable<AdofaiIpcClients["orbit"]>["renderStatus"]>>;

function mapState(state: string): RenderJob["state"] {
  switch (state.toLowerCase()) {
    case "completed":
      return "completed";
    case "failed":
      return "failed";
    case "cancelled":
      return "cancelled";
    case "finishing":
      return "compositing";
    case "rendering":
    case "awaitingconfirmation":
      return "rendering";
    default:
      return "preparing";
  }
}

function mapJob(job: OrbitJob): RenderJob {
  return {
    warnings: [],
    waitingForGameFocus: false,
    waitingForFfmpeg: false,
    jobId: job.id,
    state: mapState(job.state),
    progress: Math.min(1, Math.max(0, job.progress)),
    errorMessage: job.error,
    errorCode: null,
    errorDetails: null,
    outputFile: job.outputPath,
    outputDirectory: null,
    localOutputPath: job.outputPath,
    // OrbitRender owns its output folder and there is no folder-open IPC yet.
    canOpenOutput: false,
    canDownload: false,
  };
}

function toOrbitOptions(manifestPath: string, options: RenderOptions) {
  return {
    replayManifestPath: manifestPath,
    width: options.width,
    height: options.height,
    targetFps: options.simulationFps,
    videoFps: options.videoFps,
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
    showModOverlay: true,
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

export function createRenderApi(clients: AdofaiIpcClients): RenderApi {
  const orbit = () => {
    if (!clients.orbit)
      throw new ApiError("Install and enable OrbitRender, then restart ADOFAI.", {
        kind: "domain",
        code: "orbit_missing",
      });
    return clients.orbit;
  };
  const requireSupportedVersion = async () => {
    const state = await callRecorder(clients, (mod) => mod.readDownloadsState());
    const version = state?.renderer?.version;
    const compatibility = orbitVersionCompatibility(version);
    if (
      compatibility === "unknown" ||
      typeof version !== "string" ||
      state.renderer.status !== "ready"
    )
      throw new ApiError("Could not verify the active OrbitRender version.", {
        kind: "domain",
        code: "orbit_version_unknown",
      });
    if (compatibility === "unsupported")
      throw new ApiError("OrbitRender 2.6.0 or later is required.", {
        kind: "domain",
        code: "orbit_version_unsupported",
      });
    return version;
  };
  return {
    async getHealth() {
      if (!clients.orbit) return unavailableHealth();
      let health: Awaited<ReturnType<OrbitRenderClient["health"]>>;
      try {
        health = await clients.orbit.health();
      } catch {
        return unavailableHealth();
      }
      const version = await requireSupportedVersion();
      const state = health.renderer.toLowerCase();
      return {
        available: true,
        version,
        schemaVersion: 1 as const,
        busy:
          orbitStates.has(state) && !["idle", "completed", "failed", "cancelled"].includes(state),
        dmNoteConfigured: false,
        orbitAvailable: true,
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
    async exportBundle(runId, options, levelPath) {
      const validated = renderOptionsSchema.parse(options);
      return decodeRecorder(
        renderExportStatusSchema,
        await callRecorder(clients, (mod) =>
          mod.prepareRenderBundle({
            runId,
            ...(levelPath ? { levelPath } : {}),
            includeWebcam: validated.includeWebcam,
            includeMicrophone: validated.includeMicrophone,
          }),
        ),
      );
    },
    getExportStatus: async (jobId) =>
      decodeRecorder(
        renderExportStatusSchema,
        await callRecorder(clients, (mod) => mod.readRenderBundleState({ jobId })),
      ),
    cancelExport: async (jobId) =>
      decodeRecorder(
        renderExportStatusSchema,
        await callRecorder(clients, (mod) => mod.cancelRenderBundle({ jobId })),
      ),
    async start(manifestPath, options) {
      await requireSupportedVersion();
      const accepted = await orbit().createRender({
        options: toOrbitOptions(manifestPath, renderOptionsSchema.parse(options)),
      });
      return mapJob(await orbit().renderStatus({ id: accepted.id }));
    },
    async getStatus(jobId) {
      return mapJob(await orbit().renderStatus({ id: jobId }));
    },
    async cancel(jobId) {
      await orbit().cancelRender({ id: jobId });
      return mapJob(await orbit().renderStatus({ id: jobId }));
    },
    async prepareDownload() {
      throw new ApiError("Downloads are not used by the OrbitRender integration.", {
        kind: "domain",
        code: "download_unavailable",
      });
    },
  };
}
