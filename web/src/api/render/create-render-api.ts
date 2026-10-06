import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import type { RenderApi } from "@/api/render/render-api";
import {
  outputDirectoryCancelSchema,
  outputDirectoryChoiceSchema,
  outputDirectoryOpenSchema,
  renderDownloadSchema,
  renderExportStatusSchema,
  renderHealthSchema,
  renderJobSchema,
  renderOptionsSchema,
  renderPreferencesSchema,
  renderSettingsSchema,
} from "@/schemas/render/render-schema";
import { ApiError } from "@/shared/errors/api-error";

export function createRenderApi(clients: AdofaiIpcClients): RenderApi {
  const renderer = () => {
    if (!clients.rendererNamespace)
      throw new ApiError("Install TUFReplay-Renderer and restart ADOFAI.", {
        kind: "domain",
        code: "renderer_missing",
      });
    return clients.rendererNamespace;
  };
  return {
    getHealth: () =>
      sendDomainCommand(
        renderer(),
        "health.read",
        "renderer.health.snapshot",
        {},
        renderHealthSchema,
      ),
    getSettings: () =>
      sendDomainCommand(
        renderer(),
        "renderer.settings.read",
        "renderer.settings.changed",
        {},
        renderSettingsSchema,
      ),
    updateSettings: (options, preferences) =>
      sendDomainCommand(
        renderer(),
        "renderer.settings.change",
        "renderer.settings.changed",
        {
          ...options,
          ...(preferences ? { preferences: renderPreferencesSchema.parse(preferences) } : {}),
        },
        renderSettingsSchema,
      ),
    chooseOutputDirectory: (initialPath) =>
      sendDomainCommand(
        renderer(),
        "renderer.folder.choose",
        "renderer.folder-selection.changed",
        initialPath ? { initialPath } : {},
        outputDirectoryChoiceSchema,
      ),
    cancelOutputDirectorySelection: (selectionId) =>
      sendDomainCommand(
        renderer(),
        "renderer.folder.cancel",
        "renderer.folder-selection.cancelled",
        { selectionId },
        outputDirectoryCancelSchema,
      ),
    openOutputDirectory: (jobId) =>
      sendDomainCommand(
        renderer(),
        "renderer.folder.open",
        "renderer.folder.opened",
        jobId ? { jobId } : {},
        outputDirectoryOpenSchema,
      ),
    exportBundle(runId, options, levelPath) {
      const validated = renderOptionsSchema.parse(options);
      return sendDomainCommand(
        clients.namespace,
        "replay.render-bundle.prepare",
        "render-bundle.state.changed",
        {
          runId,
          ...(levelPath ? { levelPath } : {}),
          includeWebcam: validated.includeWebcam,
          includeMicrophone: validated.includeMicrophone,
        },
        renderExportStatusSchema,
      );
    },
    getExportStatus: (jobId) =>
      sendDomainCommand(
        clients.namespace,
        "replay.render-bundle.state.read",
        "render-bundle.state.changed",
        { jobId },
        renderExportStatusSchema,
      ),
    cancelExport: (jobId) =>
      sendDomainCommand(
        clients.namespace,
        "replay.render-bundle.cancel",
        "render-bundle.state.changed",
        { jobId },
        renderExportStatusSchema,
      ),
    start(manifestPath, options) {
      return sendDomainCommand(
        renderer(),
        "render.start",
        "renderer.job.changed",
        { manifestPath, ...renderOptionsSchema.parse(options) },
        renderJobSchema,
      );
    },
    getStatus: (jobId) =>
      sendDomainCommand(
        renderer(),
        "render.state.read",
        "renderer.job.changed",
        { jobId },
        renderJobSchema,
      ),
    cancel: (jobId) =>
      sendDomainCommand(
        renderer(),
        "render.cancel",
        "renderer.job.changed",
        { jobId },
        renderJobSchema,
      ),
    async prepareDownload(jobId) {
      const ticket = await sendDomainCommand(
        renderer(),
        "render.download",
        "download.ready",
        { jobId },
        renderDownloadSchema,
      );
      const url = new URL(ticket.url);
      if (
        url.protocol !== "http:" ||
        url.hostname !== "127.0.0.1" ||
        Number(url.port) < 32145 ||
        Number(url.port) > 32155 ||
        url.username ||
        url.password ||
        url.search ||
        url.hash ||
        !/^\/ipc\/download\/[A-Za-z0-9_-]+$/.test(url.pathname)
      )
        throw new ApiError("Invalid local render download URL.", {
          kind: "validation",
          code: "invalid_response",
        });
      return ticket.url;
    },
  };
}
