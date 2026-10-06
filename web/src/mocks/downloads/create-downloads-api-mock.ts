import type { DownloadsApi, DownloadsState } from "@/api/downloads/downloads-api";

export function createDownloadsApiMock(
  changed: (state: DownloadsState) => void = () => {},
): DownloadsApi {
  const state: DownloadsState = {
    Renderer: {
      Status: "ready",
      Version: "0.1.0-beta.1",
      InstallationDirectory: "Mods/TUFReplay-Renderer",
      DownloadedBytes: 0,
      TotalBytes: null,
    },
    Ffmpeg: {
      Status: "missing",
      InstallationDirectory: "Mods/TUFReplay/FFmpeg/windows-x64",
      DownloadedBytes: 0,
      TotalBytes: null,
    },
    CameraNeedsFfmpeg: true,
  };
  const snapshot = () => structuredClone(state);
  let tick: ReturnType<typeof setInterval> | undefined;
  return {
    async getStatus() {
      return snapshot();
    },
    async act(item, action) {
      const target = item === "renderer" ? state.Renderer : state.Ffmpeg;
      if (
        action === "request" &&
        !["ready", "restart-required", "downloading"].includes(target.Status)
      )
        target.Status = "awaiting-consent";
      if (action === "confirm" && target.Status === "awaiting-consent") {
        target.Status = "downloading";
        target.DownloadedBytes = 0;
        target.TotalBytes = 30 * 1048576;
        tick = setInterval(() => {
          target.DownloadedBytes += 6 * 1048576;
          if (target.DownloadedBytes >= (target.TotalBytes ?? 0)) {
            clearInterval(tick);
            target.Status = item === "renderer" ? "restart-required" : "ready";
          }
          changed(snapshot());
        }, 500);
      }
      if (action === "cancel") {
        clearInterval(tick);
        target.Status = "cancelled";
      }
      changed(snapshot());
      return snapshot();
    },
    async cancelPendingFfmpeg() {
      if (state.Ffmpeg.Status === "awaiting-consent") {
        state.Ffmpeg.Status = "cancelled";
        changed(snapshot());
      }
    },
  };
}
