import { orbitVersionCompatibility } from "@/models/render/orbit-version";

/** The part of the download center's OrbitRender state that decides the setup steps. */
export interface RendererInstall {
  Status: string;
  Version?: string | null;
}

/** Where someone is on the way from "no OrbitRender" to "can make videos". */
export type OrbitRenderSetupStage =
  | "checking"
  | "install"
  | "installing"
  | "install-failed"
  | "update"
  | "restart"
  | "ready";

export type SetupStepState = "upcoming" | "current" | "working" | "done" | "attention";

/** Install, restart ADOFAI, make a video: one state for each step. */
export type SetupSteps = readonly [SetupStepState, SetupStepState, SetupStepState];

const installingStatuses = new Set(["resolving", "downloading", "extracting", "validating"]);

export function orbitRenderSetupStage(install: RendererInstall): OrbitRenderSetupStage {
  if (install.Status === "checking") return "checking";
  if (install.Status === "failed") return "install-failed";
  if (install.Status === "restart-required") return "restart";
  if (install.Status === "ready")
    return orbitVersionCompatibility(install.Version) === "supported" ? "ready" : "update";
  return installingStatuses.has(install.Status) ? "installing" : "install";
}

/** True when a video can be made right away. */
export function orbitRenderReady(install: RendererInstall) {
  return orbitRenderSetupStage(install) === "ready";
}

/** Statuses where opening the setup asks the game for install consent straight away. */
export function orbitRenderNeedsRequest(status: string) {
  return status === "missing" || status === "cancelled" || status === "declined";
}

export function orbitRenderSetupSteps(stage: OrbitRenderSetupStage): SetupSteps {
  switch (stage) {
    case "checking":
    case "installing":
      return ["working", "upcoming", "upcoming"];
    case "install":
      return ["current", "upcoming", "upcoming"];
    case "install-failed":
    case "update":
      return ["attention", "upcoming", "upcoming"];
    case "restart":
      return ["done", "current", "upcoming"];
    case "ready":
      return ["done", "done", "current"];
  }
}
