import type { RenderApi } from "@/api/render/render-api";
import {
  renderDownloadSchema,
  renderExportStatusSchema,
  renderHealthSchema,
  renderJobSchema,
  renderOptionsSchema,
} from "@/schemas/render/render-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
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
    getHealth: () => callAdofaiIpc(renderer(), "health.get", {}, renderHealthSchema),
    exportBundle(runId, options, levelPath) {
      const validated = renderOptionsSchema.parse(options);
      return callAdofaiIpc(
        clients.namespace,
        "replay.render-bundle.export",
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
      callAdofaiIpc(
        clients.namespace,
        "replay.render-bundle.status.get",
        { jobId },
        renderExportStatusSchema,
      ),
    cancelExport: (jobId) =>
      callAdofaiIpc(
        clients.namespace,
        "replay.render-bundle.cancel",
        { jobId },
        renderExportStatusSchema,
      ),
    start(manifestPath, options) {
      return callAdofaiIpc(
        renderer(),
        "render.start",
        { manifestPath, ...renderOptionsSchema.parse(options) },
        renderJobSchema,
      );
    },
    getStatus: (jobId) =>
      callAdofaiIpc(renderer(), "render.status.get", { jobId }, renderJobSchema),
    cancel: (jobId) => callAdofaiIpc(renderer(), "render.cancel", { jobId }, renderJobSchema),
    async prepareDownload(jobId) {
      const ticket = await callAdofaiIpc(
        renderer(),
        "render.download",
        { jobId },
        renderDownloadSchema,
      );
      const url = new URL(ticket);
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
      return ticket;
    },
  };
}
