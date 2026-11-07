import type { DownloadCenterControl } from "@/hooks/downloads/use-download-center";
import { useRenderControl } from "@/hooks/render/use-render-control";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import type { ActivityRun } from "@/models/activity/activity-model";
import { orbitRenderNeedsRequest, orbitRenderReady } from "@/models/downloads/orbit-render-setup";

/**
 * Make a video: opens the render settings when OrbitRender can render, and the OrbitRender
 * setup from the download center when it cannot, so the next step is always one click away.
 */
export function useRenderLauncher(downloadCenter: DownloadCenterControl) {
  const showSetup = useStableCallback((opener: HTMLElement | null) => {
    const status = downloadCenter.state?.Renderer.Status;
    downloadCenter.showDetails("renderer", Boolean(status && orbitRenderNeedsRequest(status)), {
      reason: "render",
      focus: opener,
    });
  });
  const control = useRenderControl({ onRendererMissing: showSetup });
  const open = useStableCallback((run: ActivityRun, opener?: HTMLElement) => {
    const renderer = downloadCenter.state?.Renderer;
    // While the installer is still checking, the render dialog asks the game and hands off.
    if (renderer && renderer.Status !== "checking" && !orbitRenderReady(renderer))
      showSetup(opener ?? null);
    else control.open(run, opener);
  });
  return { control, open };
}
