import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import { toDownloadsStateDto } from "@/api/recorder-dtos";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import { type DownloadsApi, downloadsStateSchema } from "./downloads-api";
export function createDownloadsApi(clients: LocalAppChannels): DownloadsApi {
  return {
    getStatus: async () =>
      decodeRecorder(
        downloadsStateSchema,
        toDownloadsStateDto(await callRecorder(clients, (mod) => mod.readDownloadsState())),
      ),
    async act(item, action) {
      const state = await callRecorder(clients, (mod) => {
        if (item === "renderer") {
          if (action === "request") return mod.requestRendererDownload();
          if (action === "confirm") return mod.confirmRendererDownload();
          return mod.cancelRendererDownload();
        }
        if (action === "request") return mod.requestFfmpegDownload();
        if (action === "confirm") return mod.confirmFfmpegDownload();
        return mod.cancelFfmpegDownload();
      });
      return decodeRecorder(downloadsStateSchema, toDownloadsStateDto(state));
    },
    async cancelPendingFfmpeg() {
      await callRecorder(clients, (mod) => mod.releaseFfmpeg());
    },
  };
}
