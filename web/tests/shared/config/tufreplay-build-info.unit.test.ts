import { describe, expect, test } from "bun:test";
import { resolveTufReplayWebBuildInfo } from "@/shared/config/tufreplay-build-info";

describe("TUFReplay web build identity", () => {
  test("defaults missing configuration to the standard main site", () => {
    expect(resolveTufReplayWebBuildInfo({})).toEqual({
      flavor: "standard",
      environment: "main",
      expectedOrigin: "https://tufreplay.impl1113.dev",
      buildSha: null,
    });
  });

  test("selects the auto-submission flavor and trims an optional build SHA", () => {
    expect(
      resolveTufReplayWebBuildInfo({
        VITE_TUFREPLAY_BUILD_FLAVOR: "auto-submission",
        VITE_TUFREPLAY_BUILD_SHA: "  abc123  ",
      }),
    ).toEqual({
      flavor: "auto-submission",
      environment: "main",
      expectedOrigin: "https://tufreplay.impl1113.dev",
      buildSha: "abc123",
    });
  });

  test.each([
    ["main", "https://tufreplay.impl1113.dev"],
    ["dev", "https://tufreplay-dev.impl1113.dev"],
    ["auto-submission", "https://tufreplay-auto.impl1113.dev"],
  ] as const)("maps the %s environment to its production origin", (environment, origin) => {
    expect(resolveTufReplayWebBuildInfo({ VITE_TUFREPLAY_ENVIRONMENT: environment })).toMatchObject(
      {
        environment,
        expectedOrigin: origin,
      },
    );
  });

  test("treats invalid build configuration as the safe standard main build", () => {
    expect(
      resolveTufReplayWebBuildInfo({
        VITE_TUFREPLAY_BUILD_FLAVOR: "unknown",
        VITE_TUFREPLAY_ENVIRONMENT: "staging",
      }),
    ).toMatchObject({ flavor: "standard", environment: "main" });
  });
});
