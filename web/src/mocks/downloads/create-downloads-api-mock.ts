import type { DownloadsApi, DownloadsState, InstallState } from "@/api/downloads/downloads-api";

/** `?mock=1&mockRenderer=missing|restart|outdated|failed` starts OrbitRender in that state. */
const rendererScenarios: Record<string, Partial<InstallState>> = {
  missing: { Status: "missing", Version: null },
  restart: { Status: "restart-required" },
  outdated: { Status: "ready", Version: "2.5.0" },
  failed: {
    Status: "failed",
    Version: null,
    ErrorCode: "renderer_release_unavailable",
    Error: "GitHub returned 404 for the latest OrbitRender release.",
  },
};

function rendererScenario(): Partial<InstallState> {
  if (typeof window === "undefined") return {};
  const name = new URLSearchParams(window.location.search).get("mockRenderer");
  return (name && rendererScenarios[name]) || {};
}

export function createDownloadsApiMock(
  changed: (state: DownloadsState) => void = () => {},
): DownloadsApi {
  const state: DownloadsState = {
    Renderer: {
      Status: "ready",
      Version: "2.6.2",
      InstallationDirectory: "Mods/OrbitRender",
      DownloadedBytes: 0,
      TotalBytes: null,
      ...rendererScenario(),
    },
    Ffmpeg: {
      Status: "missing",
      InstallationDirectory: "ffmpeg/windows-x64",
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
            if (item === "renderer") target.Version = "2.6.2";
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
