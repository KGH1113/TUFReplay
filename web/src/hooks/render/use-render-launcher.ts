import { useRef, useState } from "react";
import type { DownloadCenterControl } from "@/hooks/downloads/use-download-center";
import { useRenderControl } from "@/hooks/render/use-render-control";
import { useStableCallback } from "@/hooks/shared/use-stable-callback";
import type { ActivityRun } from "@/models/activity/activity-model";
import { orbitRenderNeedsRequest, orbitRenderReady } from "@/models/downloads/orbit-render-setup";

/**
 * Make a video: asks which level file to render, like a replay does, then opens the render
 * settings when OrbitRender can render, and the OrbitRender setup from the download center
 * when it cannot, so the next step is always one click away.
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
  const [levelChoiceRun, setLevelChoiceRun] = useState<ActivityRun | null>(null);
  // The element that asked for the video; the render settings return focus to it.
  const opener = useRef<HTMLElement | null>(null);

  const open = useStableCallback((run: ActivityRun, trigger?: HTMLElement) => {
    const renderer = downloadCenter.state?.Renderer;
    // While the installer is still checking, the render dialog asks the game and hands off.
    if (renderer && renderer.Status !== "checking" && !orbitRenderReady(renderer)) {
      showSetup(trigger ?? null);
      return;
    }
    opener.current =
      trigger ?? (document.activeElement instanceof HTMLElement ? document.activeElement : null);
    setLevelChoiceRun(run);
  });

  /** Opens the render settings for the chosen file; no path renders the recorded level. */
  const chooseLevel = useStableCallback(async (runId: string, levelPath?: string) => {
    if (!levelChoiceRun || levelChoiceRun.id !== runId || control.busy) return false;
    control.open(levelChoiceRun, opener.current ?? undefined, levelPath);
    return true;
  });

  const closeLevelChoice = useStableCallback(() => setLevelChoiceRun(null));

  return { control, open, levelChoiceRun, chooseLevel, closeLevelChoice };
}
