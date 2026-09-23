import { describe, expect, it } from "bun:test";
import { createSubmissionApi } from "@/api/submission/create-submission-api";
import { createSubmissionApiMock } from "@/mocks/submission/submission-api-mock";
import { canSubmit } from "@/models/submission/submission-model";
import { submissionStatusSchema } from "@/schemas/submission/submission-schema";
import type { AdofaiIpcClients } from "@/shared/clients/adofai-ipc-client";

const compatibleHealth = {
  Ok: true,
  Mod: "TUFReplay",
  ModVersion: "0.2.0-auto-submission.1",
  ProtocolVersion: 7,
  ServerVersion: 1,
  ReplayEngineId: "tufreplay.replay.v2",
  ReplayFormatVersion: 1,
  BuildFlavor: "auto-submission",
  AutoSubmissionProtocolVersion: 2,
};

describe("auto submission boundaries", () => {
  it("fails closed when older IPC status has no trusted-tester capability", () => {
    const status = submissionStatusSchema.parse({
      connected: true,
      configured: true,
      disabled: false,
      state: "ready",
    });
    expect(status.canSubmit).toBe(false);
    expect(status.accountStatus).toBe("unavailable");
    expect(status.username).toBeNull();
    expect(status.nickname).toBeNull();
    expect(status.denialReason).toBeNull();
  });

  it("parses connected account identity and trusted-tester eligibility", () => {
    const status = submissionStatusSchema.parse({
      connected: true,
      configured: true,
      disabled: false,
      state: "ready",
      username: "impl.dev",
      nickname: "impl",
      accountStatus: "available",
      canSubmit: true,
      denialReason: null,
    });
    expect(status.username).toBe("impl.dev");
    expect(status.nickname).toBe("impl");
    expect(status.accountStatus).toBe("available");
    expect(status.canSubmit).toBe(true);
    expect(status.denialReason).toBeNull();

    const denied = submissionStatusSchema.parse({
      ...status,
      canSubmit: false,
      denialReason: "auto_submission_tester_required",
    });
    expect(denied.canSubmit).toBe(false);
    expect(denied.denialReason).toBe("auto_submission_tester_required");
  });

  it("does not validate clears until submit and preserves saved plays when capture is disabled", async () => {
    const api = createSubmissionApiMock();
    const connected = await api.connect();
    expect(connected.username).toBe("mock.tester");
    expect(connected.accountStatus).toBe("available");
    expect(connected.canSubmit).toBe(true);
    const afterCaptureDisabled = await api.setDisabled(true);
    expect(afterCaptureDisabled.canSubmit).toBe(true);
    const before = (await api.list()).runs[0];
    if (!before) throw new Error("missing fixture");
    expect(before.status).toBe("evidence_ready");
    expect(canSubmit(before)).toBe(true);
    expect(canSubmit({ ...before, evidence_expires_at: new Date(0).toISOString() })).toBe(false);
    const result = await api.submit(before.run_id);
    expect(result.status).toBe("validator_unavailable");
    expect(result.external_pass_id).toBeNull();
  });

  it("routes authentication and record operations through mod IPC without exposing tokens", async () => {
    const calls: { method: string; params: unknown }[] = [];
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        if (method === "health.get") return compatibleHealth;
        if (method === "submission.oauth.begin")
          return { authorizationUrl: "https://api.tuforums.com/oauth/authorize?state=fixture" };
        if (method === "submission.oauth.complete") return { completed: true };
        if (method === "submission.runs.list") return { runs: [], next_cursor: null };
        if (method === "submission.run.get")
          return {
            cursor: 1,
            run_id: "68727984-2424-4a6d-a72b-919044143454",
            tuf_level_id: 42,
            chart_path: "main.adofai",
            status: "evidence_ready",
            reason: null,
            external_pass_id: null,
            created_at: "2026-09-11T00:00:00Z",
            evidence_expires_at: null,
          };
        if (method === "submission.run.remove") return { deleted: true };
        if (method === "submission.account.disconnect") return { disconnected: true };
        return { connected: true, configured: true, disabled: false, state: "ready" };
      },
    };
    const clients = { namespace, pickerNamespace: namespace } as unknown as AdofaiIpcClients;
    let navigated: string | null = null;
    const api = createSubmissionApi(
      clients,
      {
        takeOAuthCallback: () => ({ code: "one-use-code", state: "mod-state" }),
        prepareOAuthWindow: () => (url: string | null) => {
          navigated = url;
        },
      },
      "auto-submission",
    );
    const status = await api.status();
    await api.status();
    expect(status.canSubmit).toBe(false);
    expect(status.accountStatus).toBe("unavailable");
    expect(calls.filter((call) => call.method === "submission.oauth.complete")).toHaveLength(1);
    const authorizing = await api.connect();
    expect(authorizing.accountStatus).toBe("unavailable");
    expect(authorizing.canSubmit).toBe(false);
    expect(String(navigated)).toContain("/oauth/authorize");
    await api.list(99);
    await api.get("68727984-2424-4a6d-a72b-919044143454");
    await api.setDisabled(true);
    await api.remove("68727984-2424-4a6d-a72b-919044143454");
    await api.disconnect();
    expect(calls).toContainEqual({ method: "submission.runs.list", params: { before: 99 } });
    expect(calls).toContainEqual({
      method: "submission.run.get",
      params: { runId: "68727984-2424-4a6d-a72b-919044143454" },
    });
    expect(calls).toContainEqual({ method: "submission.disabled.set", params: { disabled: true } });
    expect(JSON.stringify(calls)).not.toContain("access_token");
    expect(JSON.stringify(calls)).not.toContain("refresh_token");
    expect(JSON.stringify(calls)).not.toContain("ticket");
    expect(calls.some((call) => call.method === "submission.run.submit")).toBe(false);
  });

  it("blocks standard web and incompatible mod builds before any submission IPC", async () => {
    for (const flavor of ["standard", "auto-submission"] as const) {
      const calls: string[] = [];
      const namespace = {
        call: async (method: string) => {
          calls.push(method);
          return { ...compatibleHealth, BuildFlavor: "standard", AutoSubmissionProtocolVersion: 0 };
        },
      };
      const api = createSubmissionApi(
        { namespace } as unknown as AdofaiIpcClients,
        {
          takeOAuthCallback: () => null,
          prepareOAuthWindow: () => () => {},
        },
        flavor,
      );
      await expect(api.submit("68727984-2424-4a6d-a72b-919044143454")).rejects.toThrow();
      expect(calls.every((method) => method === "health.get")).toBe(true);
    }
  });

  it("rechecks mod compatibility after a successful status request", async () => {
    let compatible = true;
    const calls: string[] = [];
    const namespace = {
      call: async (method: string) => {
        calls.push(method);
        if (method === "health.get") return { ...compatibleHealth, Ok: compatible };
        return { connected: true, configured: true, disabled: false, state: "ready" };
      },
    };
    const api = createSubmissionApi(
      { namespace } as unknown as AdofaiIpcClients,
      {
        takeOAuthCallback: () => null,
        prepareOAuthWindow: () => () => {},
      },
      "auto-submission",
    );
    await api.status();
    compatible = false;
    await expect(api.submit("68727984-2424-4a6d-a72b-919044143454")).rejects.toThrow();
    expect(calls).not.toContain("submission.run.submit");
  });

  it("sends a presentation only for the first selection and omits it on retry", async () => {
    const calls: { method: string; params: unknown }[] = [];
    const run = {
      cursor: 1,
      run_id: "68727984-2424-4a6d-a72b-919044143454",
      tuf_level_id: 42,
      chart_path: "main.adofai",
      status: "evidence_ready",
      reason: null,
      external_pass_id: null,
      created_at: "2026-09-11T00:00:00Z",
      evidence_expires_at: null,
      presentation: null,
    };
    const namespace = {
      call: async (method: string, params: unknown) => {
        calls.push({ method, params });
        if (method === "health.get") return compatibleHealth;
        if (method === "submission.status.get") {
          return {
            connected: true,
            configured: true,
            disabled: false,
            state: "ready",
            accountStatus: "available",
            canSubmit: true,
            denialReason: null,
          };
        }
        if (method === "submission.run.submit") return run;
        throw new Error(`unexpected method ${method}`);
      },
    };
    const api = createSubmissionApi(
      { namespace } as unknown as AdofaiIpcClients,
      { takeOAuthCallback: () => null, prepareOAuthWindow: () => () => {} },
      "auto-submission",
    );

    const selection = { keyviewer_id: "keyviewer-1", overlay_id: null };
    await api.submit(run.run_id, selection);
    await api.submit(run.run_id);

    expect(calls).toContainEqual({
      method: "submission.run.submit",
      params: { runId: run.run_id, presentation: selection },
    });
    expect(calls).toContainEqual({
      method: "submission.run.submit",
      params: { runId: run.run_id },
    });
  });
});
