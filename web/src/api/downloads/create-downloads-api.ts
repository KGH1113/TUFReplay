import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
import { type DownloadsApi, downloadsStateSchema, installStateSchema } from "./downloads-api";

export function createDownloadsApi(clients: AdofaiIpcClients): DownloadsApi {
  return {
    getStatus: () =>
      sendDomainCommand(
        clients.namespace,
        "downloads.state.read",
        "downloads.state.changed",
        {},
        downloadsStateSchema,
      ),
    act: (item, action) =>
      sendDomainCommand(
        clients.namespace,
        `downloads.${item}.${action}`,
        "downloads.state.changed",
        {},
        downloadsStateSchema,
      ),
    async cancelPendingFfmpeg() {
      await sendDomainCommand(
        clients.namespace,
        "media.ffmpeg.release",
        "media.ffmpeg.state.changed",
        {},
        installStateSchema,
      );
    },
  };
}
