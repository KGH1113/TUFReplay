import { describe, expect, test } from "bun:test";
import {
  orbitRenderNeedsRequest,
  orbitRenderReady,
  orbitRenderSetupStage,
  orbitRenderSetupSteps,
} from "@/models/downloads/orbit-render-setup";

describe("OrbitRender setup steps", () => {
  test.each([
    ["checking", null, "checking"],
    ["missing", null, "install"],
    ["awaiting-consent", null, "install"],
    ["cancelled", null, "install"],
    ["declined", null, "install"],
    ["resolving", null, "installing"],
    ["downloading", null, "installing"],
    ["extracting", null, "installing"],
    ["validating", null, "installing"],
    ["failed", null, "install-failed"],
    ["restart-required", "2.6.2", "restart"],
    ["ready", "2.6.2", "ready"],
    ["ready", "2.7.0", "ready"],
    ["ready", "2.6.1", "update"],
    ["ready", "0.1.0-beta.1", "update"],
    ["ready", null, "update"],
  ] as const)("%s with version %s is the %s stage", (status, version, stage) => {
    expect(orbitRenderSetupStage({ Status: status, Version: version })).toBe(stage);
  });

  test("only a loaded, supported OrbitRender can render", () => {
    expect(orbitRenderReady({ Status: "ready", Version: "2.6.2" })).toBe(true);
    expect(orbitRenderReady({ Status: "ready", Version: "2.6.1" })).toBe(false);
    expect(orbitRenderReady({ Status: "restart-required", Version: "2.6.2" })).toBe(false);
    expect(orbitRenderReady({ Status: "missing" })).toBe(false);
  });

  test("marks finished steps done and the step to take now", () => {
    expect(orbitRenderSetupSteps("install")).toEqual(["current", "upcoming", "upcoming"]);
    expect(orbitRenderSetupSteps("installing")).toEqual(["working", "upcoming", "upcoming"]);
    expect(orbitRenderSetupSteps("install-failed")).toEqual(["attention", "upcoming", "upcoming"]);
    expect(orbitRenderSetupSteps("update")).toEqual(["attention", "upcoming", "upcoming"]);
    expect(orbitRenderSetupSteps("restart")).toEqual(["done", "current", "upcoming"]);
    expect(orbitRenderSetupSteps("ready")).toEqual(["done", "done", "current"]);
  });

  test("asks for install consent only when nothing is installed or in progress", () => {
    expect(orbitRenderNeedsRequest("missing")).toBe(true);
    expect(orbitRenderNeedsRequest("cancelled")).toBe(true);
    expect(orbitRenderNeedsRequest("declined")).toBe(true);
    expect(orbitRenderNeedsRequest("awaiting-consent")).toBe(false);
    expect(orbitRenderNeedsRequest("failed")).toBe(false);
    expect(orbitRenderNeedsRequest("downloading")).toBe(false);
    expect(orbitRenderNeedsRequest("restart-required")).toBe(false);
  });
});
