import { describe, expect, it } from "bun:test";
import { createVisualApi } from "@/api/visual/create-visual-api";
import type { VisualRegistrationProgress } from "@/models/visual/visual-registration-model";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";

const input = { name: "CJK keys", kind: "keyviewer" as const, source: "jipper-keyviewer" as const };
const preset = { ...input, id: "registered", source_version: "1.7.2", created_at: "2026-09-23" };

function client(responses: unknown[], loseStartResponse = false) {
  const calls: Array<{ method: string; params: Record<string, string> }> = [];
  const namespace = {
    async call(method: string, params: Record<string, string>) {
      calls.push({ method, params });
      if (method === "visual.presets.registration.start") {
        if (loseStartResponse) throw new TypeError("fetch failed");
        return { operation_id: params.operationId };
      }
      expect(method).toBe("visual.presets.registration.get");
      const response = responses.shift();
      if (response instanceof Error) throw response;
      if (!response) throw new Error("Unexpected extra poll");
      return response;
    },
  };
  return {
    calls,
    api: createVisualApi({ namespace, pickerNamespace: namespace } as unknown as AdofaiIpcClients),
  };
}

describe("background visual registration", () => {
  it("recovers a lost start response by querying the same operation", async () => {
    const { api, calls } = client(
      [{ state: "completed", progress: { stage: "uploading" }, preset }],
      true,
    );
    expect((await api.registerPreset(input)).state).toBe("completed");
    expect(calls).toHaveLength(2);
    expect(calls[0]?.params.operationId).toBe(calls[1]?.params.operationId);
  });
  it("reports measured native phases and registers through one job without a separate inspect/import", async () => {
    const processing: VisualRegistrationProgress = {
      stage: "processing_assets",
      asset_name: "cjk.otf",
      completed_assets: 2,
    };
    const uploading: VisualRegistrationProgress = { stage: "uploading", completed_assets: 4 };
    const { api, calls } = client([
      { state: "running", progress: processing },
      { state: "running", progress: uploading },
      { state: "completed", progress: uploading, preset },
    ]);
    const updates: VisualRegistrationProgress[] = [];
    expect((await api.registerPreset(input, (progress) => updates.push(progress))).state).toBe(
      "completed",
    );
    expect(updates).toEqual([processing, uploading, uploading]);
    expect(calls.map((call) => call.method)).toEqual([
      "visual.presets.registration.start",
      ...Array(3).fill("visual.presets.registration.get"),
    ]);
    expect(new Set(calls.map((call) => call.params.operationId)).size).toBe(1);
  });

  it("returns missing attachments without reporting registration success", async () => {
    const missing_assets = [{ reference: "custom.otf", kind: "font" }];
    const { api, calls } = client([
      { state: "needs_assets", progress: { stage: "validating" }, missing_assets },
    ]);
    expect(await api.registerPreset(input)).toMatchObject({
      state: "needs_assets",
      missing_assets,
    });
    expect(calls).toHaveLength(2);
  });

  it("retains the failing stage and stops polling on a worker error", async () => {
    const updates: VisualRegistrationProgress[] = [];
    const { api, calls } = client([
      {
        state: "failed",
        progress: { stage: "uploading", completed_assets: 4 },
        error: { code: "visual_name_taken" },
      },
    ]);
    await expect(
      api.registerPreset(input, (progress) => updates.push(progress)),
    ).rejects.toMatchObject({ code: "visual_name_taken" });
    expect(updates[0]?.stage).toBe("uploading");
    expect(calls).toHaveLength(2);
  });

  it("retries status reads without starting another registration", async () => {
    const { api, calls } = client([
      new TypeError("fetch failed"),
      { state: "completed", progress: { stage: "uploading" }, preset },
    ]);
    expect((await api.registerPreset(input)).state).toBe("completed");
    expect(calls.filter((call) => call.method.endsWith("start"))).toHaveLength(1);
  });
});
