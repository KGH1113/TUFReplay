import type { GameIconApi } from "@/api/game-icons/game-icon-api";
import { callRecorder } from "@/api/recorder-call";
import type { ImageDownloads } from "@/ports/image-download";
import type { LocalAppChannels } from "@/ports/local-message-peer";

export function createGameIconApi(clients: LocalAppChannels, images: ImageDownloads): GameIconApi {
  return {
    async getIcon(icon, options) {
      const result = await callRecorder(clients, (mod) => mod.readGameIcon({ icon }), options);
      return result === null ? null : images.readPng(result.file, options);
    },
  };
}
