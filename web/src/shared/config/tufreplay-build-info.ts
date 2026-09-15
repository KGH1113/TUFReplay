export const TUFREPLAY_BUILD_FLAVORS = ["standard", "auto-submission"] as const;
export const TUFREPLAY_ENVIRONMENTS = ["main", "dev", "auto-submission"] as const;

export type TufReplayBuildFlavor = (typeof TUFREPLAY_BUILD_FLAVORS)[number];
export type TufReplayEnvironment = (typeof TUFREPLAY_ENVIRONMENTS)[number];

export const TUFREPLAY_WEB_ORIGINS: Record<TufReplayEnvironment, string> = {
  main: "https://tufreplay.impl1113.dev",
  dev: "https://tufreplay-dev.impl1113.dev",
  "auto-submission": "https://tufreplay-auto.impl1113.dev",
};

export interface TufReplayWebBuildInfo {
  flavor: TufReplayBuildFlavor;
  environment: TufReplayEnvironment;
  expectedOrigin: string;
  buildSha: string | null;
}

export interface TufReplayBuildEnvironment {
  VITE_TUFREPLAY_BUILD_FLAVOR?: string;
  VITE_TUFREPLAY_ENVIRONMENT?: string;
  VITE_TUFREPLAY_BUILD_SHA?: string;
}

export function resolveTufReplayWebBuildInfo(
  environment: TufReplayBuildEnvironment = import.meta.env ?? {},
): TufReplayWebBuildInfo {
  const flavor =
    environment.VITE_TUFREPLAY_BUILD_FLAVOR === "auto-submission" ? "auto-submission" : "standard";
  const buildEnvironment = TUFREPLAY_ENVIRONMENTS.includes(
    environment.VITE_TUFREPLAY_ENVIRONMENT as TufReplayEnvironment,
  )
    ? (environment.VITE_TUFREPLAY_ENVIRONMENT as TufReplayEnvironment)
    : "main";
  const buildSha = environment.VITE_TUFREPLAY_BUILD_SHA?.trim() || null;

  return {
    flavor,
    environment: buildEnvironment,
    expectedOrigin: TUFREPLAY_WEB_ORIGINS[buildEnvironment],
    buildSha,
  };
}

export const TUFREPLAY_WEB_BUILD = resolveTufReplayWebBuildInfo();
