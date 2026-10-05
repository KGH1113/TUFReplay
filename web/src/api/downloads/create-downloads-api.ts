import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";
import { ApiError } from "@/shared/errors/api-error";
import { type DownloadsApi, downloadsStateSchema, installStateSchema } from "./downloads-api";

export function createDownloadsApi(clients: AdofaiIpcClients): DownloadsApi {
  const getStatus = async () => {
    try {
      return await callAdofaiIpc(clients.namespace, "downloads.status", {}, downloadsStateSchema);
    } catch (cause) {
      if (cause instanceof ApiError && cause.code === "handler_not_found")
        throw new ApiError(
          "Update TUFReplay and fully restart ADOFAI to use the download center.",
          { kind: "domain", code: "downloads_unavailable", cause },
        );
      throw cause;
    }
  };
  return {
    getStatus,
    async act(item, action) {
      if (item === "renderer")
        return callAdofaiIpc(
          clients.namespace,
          `downloads.renderer.${action}`,
          {},
          downloadsStateSchema,
        );
      await callAdofaiIpc(clients.namespace, `media.ffmpeg.${action}`, {}, installStateSchema);
      return getStatus();
    },
    async cancelPendingFfmpeg() {
      await callAdofaiIpc(clients.namespace, "media.ffmpeg.cancel-pending", {}, installStateSchema);
    },
  };
}
